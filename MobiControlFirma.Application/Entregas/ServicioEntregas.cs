using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MobiControlFirma.Application.Common;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Domain.Entities;
using MobiControlFirma.Domain.Enums;

namespace MobiControlFirma.Application.Entregas;

/// <summary>
/// Todo el flujo de una firma: normalizar lo que llega del dispositivo, resolver catálogos,
/// guardar la firma y el acta, y avisarle a MobiControl.
/// </summary>
public class ServicioEntregas(
    IApplicationDbContext db,
    IAlmacenamientoArchivos almacenamiento,
    IGeneradorActaPdf generadorPdf,
    IClienteMobiControl mobiControl,
    ILogger<ServicioEntregas> logger) : IServicioEntregas
{
    /// <summary>Una firma de canvas pesa unos 20 KB; más de esto no es una firma.</summary>
    private const int MaximoBytesFirma = 2 * 1024 * 1024;

    private const string ContenedorFirmas = "firmas";
    private const string ContenedorDocumentos = "documentos-pdf";

    public async Task<EntregaCreadaResponse> RegistrarAsync(
        RegistrarEntregaRequest solicitud, string? ipOrigen, string? userAgent, CancellationToken ct = default)
    {
        var deviceId = TextoMobiControl.Normalizar(solicitud.DeviceId, 100)
            ?? throw new ErrorSolicitudException("El equipo no envió su identificador de MobiControl (%deviceid%).");

        var cedula = TextoMobiControl.Normalizar(solicitud.Cedula, 20)
            ?? throw new ErrorSolicitudException("La cédula es obligatoria y llegó vacía o sin resolver.");

        var nombreFirmante = TextoMobiControl.Normalizar(solicitud.NombreAsociado, 200)
            ?? TextoMobiControl.Normalizar(solicitud.Usuario, 200)
            ?? throw new ErrorSolicitudException("El nombre del asociado es obligatorio.");

        var firmaPng = TextoMobiControl.DecodificarImagenBase64(solicitud.FirmaBase64)
            ?? throw new ErrorSolicitudException("La firma no llegó o no se pudo leer.");

        if (firmaPng.Length > MaximoBytesFirma)
            throw new ErrorSolicitudException("La imagen de la firma supera el tamaño permitido.");

        if (!EsPng(firmaPng))
            throw new ErrorSolicitudException("La firma debe enviarse como imagen PNG.");

        var claveIdempotencia = TextoMobiControl.Normalizar(solicitud.ClaveIdempotencia, 100);

        // Un reenvío tras un corte de red trae la misma clave: se devuelve el acta original en
        // vez de crear una segunda, que dejaría dos PDF y dos check-in por la misma entrega.
        if (claveIdempotencia is not null)
        {
            var previa = await db.Entregas
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ClaveIdempotencia == claveIdempotencia, ct);

            if (previa is not null)
            {
                logger.LogInformation(
                    "Reenvío del acta {Uid} con la misma clave de idempotencia; no se duplica.", previa.EntregaUid);
                return Respuesta(previa, duplicada: true, []);
            }
        }

        var (empleado, dispositivo, estadoId, canalId, distritoId) =
            await ResolverMaestrosAsync(solicitud, deviceId, cedula, nombreFirmante, ct);

        var ahora = DateTime.UtcNow;
        var costo = TextoMobiControl.ParsearMoneda(solicitud.Costo) ?? dispositivo.CostoEquipo;

        var entrega = new EntregaDispositivo
        {
            EntregaUid = Guid.NewGuid(),
            DispositivoId = dispositivo.DispositivoId,
            EmpleadoId = empleado.EmpleadoId,
            EstadoId = estadoId,
            CanalId = canalId,
            DistritoId = distritoId,
            NombreAsociadoFirmante = nombreFirmante,
            Entregables = TextoMobiControl.Normalizar(solicitud.Entregables),
            ICCID = TextoMobiControl.Normalizar(solicitud.Iccid, 50) ?? dispositivo.ICCID,
            NumeroCelular = TextoMobiControl.Normalizar(solicitud.NumeroCelular, 30) ?? dispositivo.NumeroCelular,
            CostoEquipo = costo,
            CiudadFirma = TextoMobiControl.Normalizar(solicitud.CiudadFirma, 100) ?? "Cali",
            // Igual que hacía el formulario: si nadie la manda, la entrega queda para el día siguiente.
            FechaEntregaProgramada = solicitud.FechaEntregaProgramada
                ?? DateOnly.FromDateTime(ahora.AddDays(1)),
            FechaFirma = ahora,
            FechaCreacion = ahora,
            EstadoProceso = EstadoProceso.FIRMADO,
            ClaveIdempotencia = claveIdempotencia,
            IPOrigen = ipOrigen,
            UserAgent = userAgent is { Length: > 300 } ? userAgent[..300] : userAgent,
        };

        var datosActa = new DatosActa
        {
            EntregaUid = entrega.EntregaUid,
            NombreTenedor = empleado.NombreCompleto,
            Cedula = empleado.Cedula,
            NombreAsociadoFirmante = nombreFirmante,
            Fabricante = dispositivo.Fabricante,
            Modelo = dispositivo.Modelo,
            Imei = dispositivo.IMEI,
            Iccid = entrega.ICCID,
            NumeroCelular = entrega.NumeroCelular,
            Estado = await NombreCatalogoAsync(db.EstadosDispositivo, estadoId, e => e.EstadoId, e => e.Nombre, ct),
            Canal = await NombreCatalogoAsync(db.Canales, canalId, c => c.CanalId, c => c.Nombre, ct),
            Distrito = await NombreCatalogoAsync(db.Distritos, distritoId, d => d.DistritoId, d => d.Nombre, ct),
            CostoEquipo = entrega.CostoEquipo,
            Entregables = entrega.Entregables,
            CiudadFirma = entrega.CiudadFirma,
            FechaFirma = entrega.FechaFirma,
            DeviceId = dispositivo.MobiControlDeviceId,
        };

        var actaPdf = generadorPdf.Generar(datosActa, firmaPng);

        // La ruta se arma con el año y el mes para que el contenedor no termine con cientos de
        // miles de archivos planos, y con el UID para que nunca se pisen dos actas.
        var carpeta = $"{entrega.FechaFirma:yyyy/MM}";
        var firmaGuardada = await almacenamiento.GuardarAsync(
            ContenedorFirmas, $"{carpeta}/{entrega.EntregaUid}.png", firmaPng, "image/png", ct);
        var pdfGuardado = await almacenamiento.GuardarAsync(
            ContenedorDocumentos, $"{carpeta}/{entrega.EntregaUid}.pdf", actaPdf, "application/pdf", ct);

        entrega.Firma = new Firma
        {
            NombreContenedor = firmaGuardada.Contenedor,
            RutaBlob = firmaGuardada.Ruta,
            UrlBlob = firmaGuardada.Url,
            FormatoImagen = "image/png",
            TamanoBytes = firmaGuardada.TamanoBytes,
            HashSHA256 = firmaGuardada.HashSha256,
            FechaCaptura = ahora,
        };

        entrega.DocumentoPdf = new DocumentoPdf
        {
            NombreContenedor = pdfGuardado.Contenedor,
            RutaBlob = pdfGuardado.Ruta,
            UrlBlob = pdfGuardado.Url,
            NombreArchivo = $"acta-entrega-{entrega.EntregaUid.ToString()[..8]}.pdf",
            TamanoBytes = pdfGuardado.TamanoBytes,
            HashSHA256 = pdfGuardado.HashSha256,
            FechaGeneracion = ahora,
        };

        db.Entregas.Add(entrega);
        await db.SaveChangesAsync(ct);

        // Recién aquí se llama a MobiControl: fuera de la escritura del acta, para que una
        // consola lenta o caída no se lleve por delante una firma que ya está guardada.
        var sincronizacion = solicitud.SincronizarMobiControl
            ? await SincronizarAsync(entrega, ct)
            : [];

        return Respuesta(entrega, duplicada: false, sincronizacion);
    }

    public async Task<EstadoFirmaDispositivoDto> ConsultarEstadoDispositivoAsync(
        string deviceId, CancellationToken ct = default)
    {
        var identificador = TextoMobiControl.Normalizar(deviceId, 100)
            ?? throw new ErrorSolicitudException("Identificador de dispositivo inválido.");

        var ultima = await db.Entregas
            .AsNoTracking()
            .Where(e => e.Dispositivo.MobiControlDeviceId == identificador)
            .OrderByDescending(e => e.FechaFirma)
            .Select(e => new { e.EntregaUid, e.FechaFirma, e.NombreAsociadoFirmante })
            .FirstOrDefaultAsync(ct);

        return ultima is null
            ? new EstadoFirmaDispositivoDto(identificador, false, null, null, null, null)
            : new EstadoFirmaDispositivoDto(
                identificador, true, ultima.EntregaUid, ultima.FechaFirma,
                ultima.NombreAsociadoFirmante, RutaPdf(ultima.EntregaUid));
    }

    public async Task<PaginaDto<EntregaResumenDto>> ListarAsync(
        string? busqueda, DateOnly? desde, DateOnly? hasta, string? estadoProceso,
        int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 200);

        var consulta = db.Entregas.AsNoTracking().AsQueryable();

        if (TextoMobiControl.Normalizar(busqueda) is { } termino)
        {
            consulta = consulta.Where(e =>
                e.Empleado.Cedula.Contains(termino) ||
                e.Empleado.NombreCompleto.Contains(termino) ||
                e.NombreAsociadoFirmante.Contains(termino) ||
                e.Dispositivo.MobiControlDeviceId.Contains(termino) ||
                (e.Dispositivo.IMEI != null && e.Dispositivo.IMEI.Contains(termino)));
        }

        if (desde is { } d) consulta = consulta.Where(e => e.FechaFirma >= d.ToDateTime(TimeOnly.MinValue));
        if (hasta is { } h) consulta = consulta.Where(e => e.FechaFirma < h.AddDays(1).ToDateTime(TimeOnly.MinValue));

        if (TextoMobiControl.Normalizar(estadoProceso) is { } estadoTexto)
        {
            if (!Enum.TryParse<EstadoProceso>(estadoTexto, ignoreCase: true, out var estado))
                throw new ErrorSolicitudException($"Estado de proceso desconocido: {estadoTexto}.");

            consulta = consulta.Where(e => e.EstadoProceso == estado);
        }

        var total = await consulta.CountAsync(ct);

        var items = await consulta
            .OrderByDescending(e => e.FechaFirma)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(e => new EntregaResumenDto(
                e.EntregaUid,
                e.EntregaId,
                e.Dispositivo.MobiControlDeviceId,
                e.Dispositivo.Fabricante,
                e.Dispositivo.Modelo,
                e.Dispositivo.IMEI,
                e.Empleado.Cedula,
                e.Empleado.NombreCompleto,
                e.NombreAsociadoFirmante,
                e.Estado != null ? e.Estado.Nombre : null,
                e.Canal != null ? e.Canal.Nombre : null,
                e.Distrito != null ? e.Distrito.Nombre : null,
                e.FechaFirma,
                e.EstadoProceso.ToString(),
                RutaPdf(e.EntregaUid),
                RutaFirma(e.EntregaUid)))
            .ToListAsync(ct);

        return new PaginaDto<EntregaResumenDto>(items, total, pagina, tamanoPagina);
    }

    public async Task<EntregaResumenDto?> ObtenerAsync(Guid entregaUid, CancellationToken ct = default) =>
        await db.Entregas
            .AsNoTracking()
            .Where(e => e.EntregaUid == entregaUid)
            .Select(e => new EntregaResumenDto(
                e.EntregaUid,
                e.EntregaId,
                e.Dispositivo.MobiControlDeviceId,
                e.Dispositivo.Fabricante,
                e.Dispositivo.Modelo,
                e.Dispositivo.IMEI,
                e.Empleado.Cedula,
                e.Empleado.NombreCompleto,
                e.NombreAsociadoFirmante,
                e.Estado != null ? e.Estado.Nombre : null,
                e.Canal != null ? e.Canal.Nombre : null,
                e.Distrito != null ? e.Distrito.Nombre : null,
                e.FechaFirma,
                e.EstadoProceso.ToString(),
                RutaPdf(e.EntregaUid),
                RutaFirma(e.EntregaUid)))
            .FirstOrDefaultAsync(ct);

    public async Task<ArchivoDescargado?> DescargarPdfAsync(Guid entregaUid, CancellationToken ct = default)
    {
        var documento = await db.DocumentosPdf
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Entrega.EntregaUid == entregaUid, ct);

        if (documento is null) return null;

        var contenido = await almacenamiento.LeerAsync(documento.NombreContenedor, documento.RutaBlob, ct);
        return contenido is null ? null : new ArchivoDescargado(contenido, "application/pdf", documento.NombreArchivo);
    }

    public async Task<ArchivoDescargado?> DescargarFirmaAsync(Guid entregaUid, CancellationToken ct = default)
    {
        var firma = await db.Firmas
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Entrega.EntregaUid == entregaUid, ct);

        if (firma is null) return null;

        var contenido = await almacenamiento.LeerAsync(firma.NombreContenedor, firma.RutaBlob, ct);
        return contenido is null
            ? null
            : new ArchivoDescargado(contenido, firma.FormatoImagen, $"firma-{entregaUid}.png");
    }

    public async Task<IReadOnlyList<ResultadoSincronizacionDto>> ReintentarSincronizacionAsync(
        Guid entregaUid, CancellationToken ct = default)
    {
        var entrega = await db.Entregas
            .Include(e => e.Dispositivo)
            .FirstOrDefaultAsync(e => e.EntregaUid == entregaUid, ct)
            ?? throw new ErrorSolicitudException("No existe un acta con ese identificador.");

        return await SincronizarAsync(entrega, ct);
    }

    // ------------------------------------------------------------------------------------

    /// <summary>
    /// Busca (o crea) empleado, dispositivo y catálogos. Se guardan aparte del acta a propósito:
    /// si el acta fallara después, quedan filas maestras de más y no un acta a medias.
    /// </summary>
    private async Task<(Empleado Empleado, Dispositivo Dispositivo, int? EstadoId, int? CanalId, int? DistritoId)>
        ResolverMaestrosAsync(
            RegistrarEntregaRequest solicitud, string deviceId, string cedula, string nombreFirmante,
            CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;

        var canal = await ResolverCanalAsync(TextoMobiControl.Normalizar(solicitud.Canal, 100), ct);
        var distrito = await ResolverDistritoAsync(TextoMobiControl.Normalizar(solicitud.Distrito, 100), ct);
        var estado = await ResolverEstadoAsync(TextoMobiControl.Normalizar(solicitud.Estado, 50), ct);

        var nombreCompleto = TextoMobiControl.Normalizar(solicitud.Usuario, 200) ?? nombreFirmante;

        var empleado = await db.Empleados.FirstOrDefaultAsync(e => e.Cedula == cedula, ct);
        if (empleado is null)
        {
            empleado = new Empleado
            {
                Cedula = cedula,
                NombreCompleto = nombreCompleto,
                FechaCreacion = ahora,
            };
            db.Empleados.Add(empleado);
        }
        else
        {
            empleado.NombreCompleto = nombreCompleto;
            empleado.FechaActualizacion = ahora;
        }

        empleado.Canal = canal ?? empleado.Canal;
        empleado.Distrito = distrito ?? empleado.Distrito;

        var dispositivo = await db.Dispositivos
            .FirstOrDefaultAsync(d => d.MobiControlDeviceId == deviceId, ct);

        if (dispositivo is null)
        {
            dispositivo = new Dispositivo
            {
                MobiControlDeviceId = deviceId,
                FechaCreacion = ahora,
            };
            db.Dispositivos.Add(dispositivo);
        }
        else
        {
            dispositivo.FechaActualizacion = ahora;
        }

        // Los datos del equipo se refrescan con lo que reporta MobiControl, pero solo cuando
        // llegan: un atributo que no se resolvió no debe borrar lo que ya se sabía del equipo.
        dispositivo.Fabricante = TextoMobiControl.Normalizar(solicitud.Fabricante, 100) ?? dispositivo.Fabricante;
        dispositivo.Modelo = TextoMobiControl.Normalizar(solicitud.Modelo, 100) ?? dispositivo.Modelo;
        dispositivo.IMEI = TextoMobiControl.Normalizar(solicitud.Imei, 50) ?? dispositivo.IMEI;
        dispositivo.ICCID = TextoMobiControl.Normalizar(solicitud.Iccid, 50) ?? dispositivo.ICCID;
        dispositivo.NumeroCelular = TextoMobiControl.Normalizar(solicitud.NumeroCelular, 30) ?? dispositivo.NumeroCelular;
        dispositivo.CostoEquipo = TextoMobiControl.ParsearMoneda(solicitud.Costo) ?? dispositivo.CostoEquipo;
        dispositivo.EstadoActual = estado ?? dispositivo.EstadoActual;

        await db.SaveChangesAsync(ct);

        return (empleado, dispositivo, estado?.EstadoId, canal?.CanalId, distrito?.DistritoId);
    }

    private async Task<Canal?> ResolverCanalAsync(string? nombre, CancellationToken ct)
    {
        if (nombre is null) return null;

        var canal = await db.Canales.FirstOrDefaultAsync(c => c.Nombre == nombre, ct);
        if (canal is not null) return canal;

        canal = new Canal { Nombre = nombre };
        db.Canales.Add(canal);
        return canal;
    }

    private async Task<Distrito?> ResolverDistritoAsync(string? nombre, CancellationToken ct)
    {
        if (nombre is null) return null;

        var distrito = await db.Distritos.FirstOrDefaultAsync(d => d.Nombre == nombre, ct);
        if (distrito is not null) return distrito;

        distrito = new Distrito { Nombre = nombre };
        db.Distritos.Add(distrito);
        return distrito;
    }

    private async Task<EstadoDispositivo?> ResolverEstadoAsync(string? nombre, CancellationToken ct)
    {
        if (nombre is null) return null;

        var estado = await db.EstadosDispositivo.FirstOrDefaultAsync(e => e.Nombre == nombre, ct);
        if (estado is not null) return estado;

        estado = new EstadoDispositivo { Nombre = nombre };
        db.EstadosDispositivo.Add(estado);
        return estado;
    }

    private async Task<string?> NombreCatalogoAsync<T>(
        DbSet<T> conjunto, int? id, Func<T, int> selectorId, Func<T, string> selectorNombre, CancellationToken ct)
        where T : class
    {
        if (id is null) return null;

        // Los catálogos recién creados todavía viven en el rastreador local del contexto; se
        // busca ahí primero para no depender de un viaje extra a la base.
        var local = conjunto.Local.FirstOrDefault(x => selectorId(x) == id);
        if (local is not null) return selectorNombre(local);

        var persistido = await conjunto.FindAsync([id.Value], ct);
        return persistido is null ? null : selectorNombre(persistido);
    }

    /// <summary>
    /// Llama a MobiControl, deja cada intento en la bitácora y ajusta el estado del acta.
    /// Nunca lanza: una firma guardada no se pierde porque la consola no conteste.
    /// </summary>
    private async Task<IReadOnlyList<ResultadoSincronizacionDto>> SincronizarAsync(
        EntregaDispositivo entrega, CancellationToken ct)
    {
        var deviceId = entrega.Dispositivo?.MobiControlDeviceId
            ?? await db.Dispositivos
                .Where(d => d.DispositivoId == entrega.DispositivoId)
                .Select(d => d.MobiControlDeviceId)
                .FirstAsync(ct);

        var fecha = entrega.FechaEntregaProgramada ?? DateOnly.FromDateTime(entrega.FechaFirma.AddDays(1));

        IReadOnlyList<ResultadoIntegracion> resultados;
        try
        {
            resultados = await mobiControl.MarcarEntregaFirmadaAsync(deviceId, fecha, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo inesperado sincronizando el acta {Uid} con MobiControl.", entrega.EntregaUid);
            resultados = [new ResultadoIntegracion(TipoAccionIntegracion.ActualizarAtributos, false, null, ex.Message)];
        }

        var ahora = DateTime.UtcNow;
        foreach (var resultado in resultados)
        {
            db.Sincronizaciones.Add(new IntegracionSincronizacion
            {
                EntregaId = entrega.EntregaId,
                Proveedor = ProveedorIntegracion.MOBICONTROL,
                TipoAccion = resultado.Accion,
                Exitoso = resultado.Exitoso,
                CodigoRespuestaHttp = resultado.CodigoHttp,
                MensajeError = resultado.MensajeError,
                FechaEjecucion = ahora,
            });
        }

        // Solo cuenta como sincronizada si el atributo quedó escrito. El check-in es un empujón
        // para que la consola refresque; que falle no invalida la marca.
        var atributosOk = resultados.Any(r =>
            r.Accion == TipoAccionIntegracion.ActualizarAtributos && r.Exitoso);

        var entregaRastreada = await db.Entregas.FirstAsync(e => e.EntregaId == entrega.EntregaId, ct);
        entregaRastreada.EstadoProceso = atributosOk ? EstadoProceso.SINCRONIZADO : EstadoProceso.ERROR_SINCRONIZACION;
        entrega.EstadoProceso = entregaRastreada.EstadoProceso;

        await db.SaveChangesAsync(ct);

        return [.. resultados.Select(r => new ResultadoSincronizacionDto(
            nameof(ProveedorIntegracion.MOBICONTROL), r.Accion, r.Exitoso, r.CodigoHttp, r.MensajeError))];
    }

    private static EntregaCreadaResponse Respuesta(
        EntregaDispositivo entrega, bool duplicada, IReadOnlyList<ResultadoSincronizacionDto> sincronizacion) =>
        new(entrega.EntregaUid,
            entrega.EntregaId,
            entrega.FechaFirma,
            entrega.EstadoProceso.ToString(),
            RutaPdf(entrega.EntregaUid),
            RutaFirma(entrega.EntregaUid),
            duplicada,
            sincronizacion);

    private static string RutaPdf(Guid uid) => $"/api/v1/entregas/{uid}/pdf";

    private static string RutaFirma(Guid uid) => $"/api/v1/entregas/{uid}/firma";

    /// <summary>Firma del formato PNG. Evita que se guarde cualquier binario disfrazado de firma.</summary>
    private static bool EsPng(byte[] contenido) =>
        contenido.Length > 8 &&
        contenido[0] == 0x89 && contenido[1] == 0x50 && contenido[2] == 0x4E && contenido[3] == 0x47;
}
