using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MobiControlFirma.Application.Common;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Application.Entregas;
using MobiControlFirma.Domain.Entities;
using MobiControlFirma.Domain.Enums;

namespace MobiControlFirma.Application.Solicitudes;

/// <summary>
/// Firma por enlace. El acta se registra con el mismo caso de uso que la del equipo —mismo PDF,
/// misma sincronización con MobiControl, misma copia por correo—; lo único que cambia es de
/// dónde salen los datos: del sistema de origen en vez de las variables de MobiControl.
/// </summary>
public class ServicioSolicitudes(
    IApplicationDbContext db,
    IContextoEmpresa empresa,
    IServicioEntregas entregas,
    IEnlacesFirma enlaces,
    IProveedorConfiguracion configuracion,
    IClienteMobiControl mobiControl,
    ILogger<ServicioSolicitudes> logger) : IServicioSolicitudes
{
    private const int VigenciaPorDefectoDias = 7;

    /// <summary>Estado que ve el origen y el formulario. El vencimiento se deduce de la fecha.</summary>
    private const string EstadoVencida = "VENCIDA";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SolicitudCreadaResponse> CrearAsync(
        CrearSolicitudRequest solicitud, CancellationToken ct = default)
    {
        // Con la llave de administrador o un usuario de plataforma sin empresa elegida no hay a
        // quién asignarle la solicitud; mejor decirlo que reventar con un error genérico.
        var empresaId = empresa.EmpresaId
            ?? throw new ErrorSolicitudException(
                "La solicitud necesita una empresa: usa la llave de la empresa o elige una en la consola.");

        var idOrigen = TextoMobiControl.Normalizar(solicitud.IdSolicitud, 100)
            ?? throw new ErrorSolicitudException("Falta el identificador de la solicitud (idSolicitud).");

        // Se exigen aquí y no al firmar: si faltan, el asociado abriría un enlace que no puede
        // terminar, y el origen no se enteraría hasta que alguien reclame.
        var deviceId = TextoMobiControl.Normalizar(solicitud.DeviceId, 100);
        var imei = TextoMobiControl.Normalizar(solicitud.Imei, 50);
        var serial = TextoMobiControl.Normalizar(solicitud.Serial, 100);

        if (deviceId is null && imei is null && serial is null)
            throw new ErrorSolicitudException(
                "Falta con qué identificar el equipo: envíe el IMEI, el serial o el deviceId de MobiControl.");

        var cedula = TextoMobiControl.Normalizar(solicitud.Cedula, 20)
            ?? throw new ErrorSolicitudException("Falta la cédula del asociado.");

        // Pedir los datos de MobiControl es decir que el equipo está allí.
        if (solicitud is { DatosDesdeMobicontrol: true, DispositivoConMobicontrol: false })
            throw new ErrorSolicitudException(
                "datosDesdeMobicontrol es SI pero dispositivoConMobicontrol es NO: sin MobiControl no hay de dónde tomar los datos.");

        var conMobiControl = solicitud.DispositivoConMobicontrol ?? (solicitud.DatosDesdeMobicontrol == true ? true : null);
        var desdeMobiControl = solicitud.DatosDesdeMobicontrol == true;

        var fabricante = TextoMobiControl.Normalizar(solicitud.Fabricante, 100);
        var modelo = TextoMobiControl.Normalizar(solicitud.Modelo, 100);
        string? nombreDispositivo = null;

        // Se valida al recibir la solicitud y no al firmar: un IMEI mal escrito en el origen se
        // corrige allí en el momento, en vez de descubrirse cuando el equipo no encuentra su acta.
        if (conMobiControl == true)
        {
            var equipo = await BuscarEquipoAsync(deviceId, imei, serial, ct)
                ?? throw new ErrorSolicitudException(
                    $"El equipo no está en MobiControl: no hay ninguno con {Descripcion(deviceId, imei, serial)}. " +
                    "Revise el dato o envíe dispositivoConMobicontrol: NO.");

            // El deviceId se guarda siempre: es con el que se marca la entrega en la consola.
            deviceId = equipo.DeviceId;
            imei ??= TextoMobiControl.Normalizar(equipo.Imei, 50);
            serial ??= TextoMobiControl.Normalizar(equipo.Serial, 100);

            if (desdeMobiControl)
            {
                fabricante = TextoMobiControl.Normalizar(equipo.Fabricante, 100) ?? fabricante;
                modelo = TextoMobiControl.Normalizar(equipo.Modelo, 100) ?? modelo;
                nombreDispositivo = TextoMobiControl.Normalizar(equipo.Nombre, 200);
            }
        }

        // La ciudad del acta es de la empresa, no del equipo: si el origen no la manda se toma
        // la que la empresa tiene en One, que es la misma que usaría el formulario del equipo.
        var ciudad = TextoMobiControl.Normalizar(solicitud.CiudadFirma, 100)
            ?? (await configuracion.ObtenerAsync(empresaId, ct))?.CiudadFirma;

        var datos = new DatosSolicitud(
            DeviceId: deviceId,
            Cedula: cedula,
            Usuario: TextoMobiControl.Normalizar(solicitud.Usuario, 200),
            Correo: TextoMobiControl.Normalizar(solicitud.Correo, 200),
            Fabricante: fabricante,
            Modelo: modelo,
            Imei: imei,
            Iccid: TextoMobiControl.Normalizar(solicitud.Iccid, 50),
            NumeroCelular: TextoMobiControl.Normalizar(solicitud.NumeroCelular, 30),
            Estado: TextoMobiControl.Normalizar(solicitud.Estado, 50),
            Canal: TextoMobiControl.Normalizar(solicitud.Canal, 100),
            Distrito: TextoMobiControl.Normalizar(solicitud.Distrito, 100),
            Costo: TextoMobiControl.Normalizar(solicitud.Costo, 50),
            Entregables: TextoMobiControl.Normalizar(solicitud.Entregables),
            CiudadFirma: ciudad,
            TipoDispositivo: TextoMobiControl.Normalizar(solicitud.TipoDispositivo, 30),
            Serial: serial,
            FechaEntrega: solicitud.FechaEntrega,
            NombreDispositivo: nombreDispositivo,
            DispositivoConMobicontrol: conMobiControl,
            DatosDesdeMobicontrol: solicitud.DatosDesdeMobicontrol,
            NombreDeInterfaz: TextoMobiControl.Normalizar(solicitud.NombreDeInterfaz, 50));

        var ahora = DateTime.UtcNow;
        var vence = ahora.AddDays(solicitud.VigenciaDias ?? VigenciaPorDefectoDias);

        var existente = await db.Solicitudes.FirstOrDefaultAsync(s => s.IdSolicitudOrigen == idOrigen, ct);

        if (existente is not null)
        {
            // Mientras nadie la firme, reenviarla es corregirla: el origen arregla un dato en su
            // sistema y lo vuelve a mandar, y el equipo o el enlace muestran ya lo corregido.
            // Firmada o rechazada ya no cambia: el acta dice lo que se firmó.
            if (existente.Estado == EstadoSolicitud.PENDIENTE)
            {
                existente.DatosOrigen = JsonSerializer.Serialize(datos, Json);
                existente.Imei = ClaveEquipo(imei, 50);
                existente.Serial = ClaveEquipo(serial, 100);
                existente.FechaVencimiento = vence;
                existente.FechaActualizacion = ahora;
                await db.SaveChangesAsync(ct);

                logger.LogInformation("El origen reenvió la solicitud {Origen}; se actualizaron sus datos.", idOrigen);
            }

            return Creada(existente, duplicada: true);
        }

        var nueva = new SolicitudFirma
        {
            EmpresaId = empresaId,
            SolicitudUid = Guid.NewGuid(),
            IdSolicitudOrigen = idOrigen,
            DatosOrigen = JsonSerializer.Serialize(datos, Json),
            Imei = ClaveEquipo(imei, 50),
            Serial = ClaveEquipo(serial, 100),
            Estado = EstadoSolicitud.PENDIENTE,
            FechaCreacion = ahora,
            FechaActualizacion = ahora,
            FechaVencimiento = vence,
        };

        db.Solicitudes.Add(nueva);
        await db.SaveChangesAsync(ct);

        // El envío del enlace por correo todavía no está: por ahora el origen recibe la URL en
        // la respuesta y es quien se la hace llegar al asociado.
        return Creada(nueva, duplicada: false);
    }

    public async Task<SolicitudDto?> ConsultarAsync(string idSolicitud, CancellationToken ct = default)
    {
        var solicitud = await BuscarCompletaAsync(idSolicitud, ct);
        return solicitud is null ? null : AVista(solicitud);
    }

    public async Task<PaginaDto<SolicitudResumenDto>> ListarAsync(
        DateTimeOffset? desde, string? estado, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        pagina = Math.Max(pagina, 1);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 200);

        var ahora = DateTime.UtcNow;
        var consulta = db.Solicitudes.AsNoTracking();

        if (desde is { } d)
        {
            var desdeUtc = d.UtcDateTime;
            consulta = consulta.Where(s => s.FechaActualizacion >= desdeUtc);
        }

        // VENCIDA no se guarda: es una pendiente a la que se le pasó la fecha.
        consulta = estado?.Trim().ToUpperInvariant() switch
        {
            null or "" => consulta,
            EstadoVencida => consulta.Where(s => s.Estado == EstadoSolicitud.PENDIENTE && s.FechaVencimiento <= ahora),
            "PENDIENTE" => consulta.Where(s => s.Estado == EstadoSolicitud.PENDIENTE && s.FechaVencimiento > ahora),
            var otro when Enum.TryParse<EstadoSolicitud>(otro, out var e) => consulta.Where(s => s.Estado == e),
            _ => throw new ErrorSolicitudException("Estado inválido: use PENDIENTE, FIRMADA, RECHAZADA o VENCIDA."),
        };

        var total = await consulta.CountAsync(ct);

        // Del cambio más viejo al más nuevo: quien sincroniza guarda la fecha del último que
        // procesó y la manda como "desde" en la siguiente vuelta.
        var filas = await consulta
            .OrderBy(s => s.FechaActualizacion).ThenBy(s => s.SolicitudId)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(ct);

        var items = filas
            .Select(s => new SolicitudResumenDto(
                s.IdSolicitudOrigen, EstadoVisible(s), Utc(s.FechaCreacion), Utc(s.FechaActualizacion),
                Utc(s.FechaFirma), Utc(s.FechaRechazo)))
            .ToList();

        return new PaginaDto<SolicitudResumenDto>(items, total, pagina, tamanoPagina);
    }

    public async Task<PrecargaEquipoDto?> BuscarPrecargaAsync(string? imei, string? serial, CancellationToken ct = default)
    {
        var claveImei = ClaveEquipo(imei, 50);
        var claveSerial = ClaveEquipo(serial, 100);

        if (claveImei is null && claveSerial is null)
            throw new ErrorSolicitudException("Envíe el IMEI o el serial del equipo.");

        var ahora = DateTime.UtcNow;

        // Si el origen pidió dos actas para el mismo equipo, gana la más reciente: es la de la
        // entrega que está por hacerse.
        var solicitud = await db.Solicitudes
            .AsNoTracking()
            .Where(s => s.Estado == EstadoSolicitud.PENDIENTE && s.FechaVencimiento > ahora)
            .Where(s => (claveImei != null && s.Imei == claveImei) || (claveSerial != null && s.Serial == claveSerial))
            .OrderByDescending(s => s.FechaActualizacion)
            .FirstOrDefaultAsync(ct);

        return solicitud is null
            ? null
            : new PrecargaEquipoDto(solicitud.IdSolicitudOrigen, solicitud.SolicitudUid, Utc(solicitud.FechaVencimiento), LeerDatos(solicitud));
    }

    public async Task<EntregaCreadaResponse> RegistrarDesdeEquipoAsync(
        RegistrarEntregaRequest acta, string? ipOrigen, string? userAgent, CancellationToken ct = default)
    {
        var solicitud = await BuscarPorOrigenAsync(acta.IdSolicitud ?? string.Empty, ct);

        // La fecha de entrega es del origen: el formulario la trae de la precarga, pero si no la
        // mandó (un acta que esperaba en la cola del equipo) se toma de la solicitud.
        if (solicitud is not null)
            acta.FechaEntregaProgramada ??= LeerDatos(solicitud).FechaEntrega;

        // El acta se registra pase lo que pase con la solicitud: la firma ya se hizo en el equipo
        // y perderla por un problema de la solicitud sería peor que un acta sin atar.
        var registro = await entregas.RegistrarAsync(acta, ipOrigen, userAgent, ct);

        if (solicitud is null)
        {
            logger.LogWarning("El equipo firmó con la solicitud {Origen}, que no existe; el acta {Acta} queda sin atar.",
                acta.IdSolicitud, registro.EntregaUid);
            return registro;
        }

        if (solicitud.Estado == EstadoSolicitud.PENDIENTE)
        {
            solicitud.Estado = EstadoSolicitud.FIRMADA;
            solicitud.EntregaId = registro.EntregaId;
            solicitud.FechaFirma = registro.FechaFirma;
            solicitud.FechaActualizacion = DateTime.UtcNow;
            solicitud.EstadoCallback = EstadoCallback.PENDIENTE;
            solicitud.ProximoIntentoCallback = null;

            await db.SaveChangesAsync(ct);

            logger.LogInformation("Solicitud {Origen} firmada desde el equipo; acta {Acta}.",
                solicitud.IdSolicitudOrigen, registro.EntregaUid);
        }
        else if (solicitud.EntregaId != registro.EntregaId)
        {
            logger.LogWarning(
                "El equipo firmó la solicitud {Origen}, que ya estaba {Estado}; el acta {Acta} queda sin atar.",
                solicitud.IdSolicitudOrigen, solicitud.Estado, registro.EntregaUid);
        }

        return registro;
    }

    public async Task<ArchivoDescargado?> DescargarPdfPorOrigenAsync(string idSolicitud, CancellationToken ct = default)
    {
        var solicitud = await BuscarPorOrigenAsync(idSolicitud, ct);
        return solicitud?.Entrega is { } entrega ? await entregas.DescargarPdfAsync(entrega.EntregaUid, ct) : null;
    }

    public async Task<SolicitudDto> ReintentarCallbackAsync(string idSolicitud, CancellationToken ct = default)
    {
        var solicitud = await BuscarCompletaAsync(idSolicitud, ct)
            ?? throw new ErrorSolicitudException("No existe una solicitud con ese identificador.");

        if (solicitud.Estado == EstadoSolicitud.PENDIENTE)
            throw new ErrorSolicitudException("La solicitud todavía no se ha firmado ni rechazado: no hay nada que avisar.");

        // Los intentos se reinician: si se reintenta a mano es porque se corrigió algo (casi
        // siempre la URL del callback en One) y merece la escalera completa otra vez.
        solicitud.EstadoCallback = EstadoCallback.PENDIENTE;
        solicitud.IntentosCallback = 0;
        solicitud.ProximoIntentoCallback = null;

        await db.SaveChangesAsync(ct);
        return AVista(solicitud);
    }

    public async Task<FormularioFirmaDto> ObtenerFormularioAsync(Guid solicitudUid, CancellationToken ct = default)
    {
        var solicitud = await db.Solicitudes
            .AsNoTracking()
            .Include(s => s.Empresa)
            .Include(s => s.Entrega)
            .FirstOrDefaultAsync(s => s.SolicitudUid == solicitudUid, ct)
            ?? throw new ErrorSolicitudException("El enlace no corresponde a ninguna solicitud.");

        return new FormularioFirmaDto(
            EstadoVisible(solicitud),
            solicitud.Empresa.Nombre,
            Utc(solicitud.FechaVencimiento),
            LeerDatos(solicitud),
            solicitud.Entrega?.EntregaUid,
            Utc(solicitud.FechaFirma),
            solicitud.Entrega?.NombreAsociadoFirmante,
            Utc(solicitud.FechaRechazo),
            solicitud.MotivoRechazo);
    }

    public async Task<FirmaRegistradaResponse> FirmarAsync(
        Guid solicitudUid, FirmarSolicitudRequest firma, string? ipOrigen, string? userAgent,
        CancellationToken ct = default)
    {
        var solicitud = await db.Solicitudes
            .Include(s => s.Entrega)
            .FirstOrDefaultAsync(s => s.SolicitudUid == solicitudUid, ct)
            ?? throw new ErrorSolicitudException("El enlace no corresponde a ninguna solicitud.");

        // Un doble clic o un reintento tras un corte de red no es un error: el asociado ya firmó
        // y lo que necesita es ver su acta, no un mensaje de rechazo.
        if (solicitud is { Estado: EstadoSolicitud.FIRMADA, Entrega: { } previa })
            return new FirmaRegistradaResponse(previa.EntregaUid, Utc(previa.FechaFirma), previa.EstadoProceso.ToString(), true);

        if (solicitud.Estado == EstadoSolicitud.RECHAZADA)
            throw new ErrorSolicitudException("Esta acta fue rechazada; ya no se puede firmar.");

        if (solicitud.FechaVencimiento <= DateTime.UtcNow)
            throw new ErrorSolicitudException(
                "Este enlace de firma venció. Pide a quien te lo envió que genere uno nuevo.");

        var datos = LeerDatos(solicitud);

        // Sin DeviceId se busca el equipo en MobiControl por lo que el asociado confirmó, salvo
        // que el origen haya dicho que el equipo no está en MobiControl.
        var conMobiControl = datos.DispositivoConMobicontrol != false;
        var deviceId = datos.DeviceId
            ?? (conMobiControl ? await BuscarEnMobiControlAsync(firma.Imei ?? datos.Imei, firma.Serial ?? datos.Serial, ct) : null);

        var registro = await entregas.RegistrarAsync(new RegistrarEntregaRequest
        {
            // El DeviceId sale de la solicitud o de MobiControl, nunca del formulario: es con lo
            // que se marca la entrega, y el asociado no debe poder cambiarlo.
            DeviceId = deviceId,
            TipoDispositivo = firma.TipoDispositivo,
            Serial = firma.Serial,
            Cedula = firma.Cedula ?? string.Empty,
            Usuario = firma.Usuario,
            NombreAsociado = firma.NombreAsociado,
            Correo = firma.Correo,
            Fabricante = firma.Fabricante,
            Modelo = firma.Modelo,
            Imei = firma.Imei,
            Iccid = firma.Iccid,
            NumeroCelular = firma.NumeroCelular,
            Estado = firma.Estado,
            Canal = firma.Canal,
            Distrito = firma.Distrito,
            Costo = firma.Costo,
            Entregables = firma.Entregables,
            CiudadFirma = TextoMobiControl.Normalizar(firma.CiudadFirma, 100) ?? datos.CiudadFirma ?? "Cali",
            FechaEntregaProgramada = datos.FechaEntrega,
            FirmaBase64 = firma.FirmaBase64,
            // Atada a la solicitud y no al envío: aunque el formulario mande dos veces, o se
            // caiga el servidor entre guardar el acta y marcar la solicitud, sale una sola acta.
            ClaveIdempotencia = $"solicitud:{solicitud.SolicitudUid:N}",
            // Aquí no hay formulario en el equipo que esperar a que se cierre: la entrega se
            // marca en MobiControl en cuanto queda firmada.
            SincronizarMobiControl = conMobiControl,
            // Va en el asunto del correo con el acta.
            IdSolicitud = solicitud.IdSolicitudOrigen,
        }, ipOrigen, userAgent, ct);

        solicitud.Estado = EstadoSolicitud.FIRMADA;
        solicitud.EntregaId = registro.EntregaId;
        solicitud.FechaFirma = registro.FechaFirma;
        solicitud.FechaActualizacion = DateTime.UtcNow;
        solicitud.EstadoCallback = EstadoCallback.PENDIENTE;
        solicitud.ProximoIntentoCallback = null;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Solicitud {Origen} firmada; acta {Acta}. El aviso al origen queda en la bandeja.",
            solicitud.IdSolicitudOrigen, registro.EntregaUid);

        return new FirmaRegistradaResponse(
            registro.EntregaUid, registro.FechaFirma, registro.EstadoProceso, registro.Duplicada);
    }

    public async Task<RechazoRegistradoResponse> RechazarAsync(
        Guid solicitudUid, RechazarSolicitudRequest rechazo, CancellationToken ct = default)
    {
        var solicitud = await db.Solicitudes
            .FirstOrDefaultAsync(s => s.SolicitudUid == solicitudUid, ct)
            ?? throw new ErrorSolicitudException("El enlace no corresponde a ninguna solicitud.");

        // Un doble clic no es un segundo rechazo: se devuelve el que ya quedó.
        if (solicitud is { Estado: EstadoSolicitud.RECHAZADA, FechaRechazo: { } fecha })
            return new RechazoRegistradoResponse(Utc(fecha), true);

        if (solicitud.Estado == EstadoSolicitud.FIRMADA)
            throw new ErrorSolicitudException("Esta acta ya fue firmada; no se puede rechazar.");

        if (solicitud.FechaVencimiento <= DateTime.UtcNow)
            throw new ErrorSolicitudException("Este enlace venció.");

        var motivo = TextoMobiControl.Normalizar(rechazo.Motivo, 500)
            ?? throw new ErrorSolicitudException("Cuéntanos por qué no aceptas el acta.");

        solicitud.Estado = EstadoSolicitud.RECHAZADA;
        solicitud.FechaRechazo = DateTime.UtcNow;
        solicitud.MotivoRechazo = motivo;
        solicitud.RechazadoPor = TextoMobiControl.Normalizar(rechazo.Nombre, 200);
        solicitud.FechaActualizacion = solicitud.FechaRechazo.Value;
        solicitud.EstadoCallback = EstadoCallback.PENDIENTE;
        solicitud.ProximoIntentoCallback = null;

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Solicitud {Origen} rechazada por el asociado. Se avisa al origen.", solicitud.IdSolicitudOrigen);
        return new RechazoRegistradoResponse(Utc(solicitud.FechaRechazo.Value), false);
    }

    public async Task<ArchivoDescargado?> DescargarPdfAsync(Guid solicitudUid, CancellationToken ct = default)
    {
        var entregaUid = await db.Solicitudes
            .AsNoTracking()
            .Where(s => s.SolicitudUid == solicitudUid && s.Entrega != null)
            .Select(s => (Guid?)s.Entrega!.EntregaUid)
            .FirstOrDefaultAsync(ct);

        return entregaUid is { } uid ? await entregas.DescargarPdfAsync(uid, ct) : null;
    }

    // ------------------------------------------------------------------------------------

    /// <summary>
    /// DeviceId del equipo en la consola de la empresa, buscado por IMEI o serial. MobiControl no
    /// filtra por IMEI, así que se recorre la flota (una llamada por cada 500 equipos). Null si no
    /// está o si la empresa no tiene consola: el acta se firma igual, solo que sin marcar el equipo.
    /// </summary>
    private async Task<string?> BuscarEnMobiControlAsync(string? imei, string? serial, CancellationToken ct)
    {
        if (ClaveEquipo(imei, 50) is null && ClaveEquipo(serial, 100) is null) return null;

        try
        {
            var equipo = await BuscarEquipoAsync(null, imei, serial, ct);

            if (equipo is null)
                logger.LogInformation("El equipo de la solicitud no está en MobiControl; el acta no lo marcará.");

            return equipo?.DeviceId;
        }
        catch (ErrorSolicitudException ex)
        {
            logger.LogWarning("No se pudo buscar el equipo en MobiControl: {Motivo}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// El equipo en la consola de la empresa, por deviceId, IMEI o serial, en ese orden. MobiControl
    /// no filtra por IMEI, así que se recorre la flota. Lanza ErrorSolicitudException si la empresa
    /// no tiene consola o MobiControl no responde.
    /// </summary>
    private async Task<EquipoMobiControl?> BuscarEquipoAsync(string? deviceId, string? imei, string? serial, CancellationToken ct)
    {
        var claveImei = ClaveEquipo(imei, 50);
        var claveSerial = ClaveEquipo(serial, 100);

        var flota = await mobiControl.ListarEquiposAsync(ct);

        return flota.FirstOrDefault(e => deviceId is not null && string.Equals(e.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
               ?? flota.FirstOrDefault(e => claveImei is not null && ClaveEquipo(e.Imei, 50) == claveImei)
               ?? flota.FirstOrDefault(e => claveSerial is not null && ClaveEquipo(e.Serial, 100) == claveSerial);
    }

    private static string Descripcion(string? deviceId, string? imei, string? serial) =>
        string.Join(" ni ", new[]
        {
            imei is null ? null : $"IMEI {imei}",
            serial is null ? null : $"serial {serial}",
            deviceId is null ? null : $"deviceId {deviceId}",
        }.Where(x => x is not null));

    /// <summary>
    /// IMEI o serial reducidos a letras y dígitos, que es como se guardan y se buscan: el origen
    /// los escribe "35 678901 234567 8" o "R58M-1234" y MobiControl los reporta sin separadores.
    /// </summary>
    private static string? ClaveEquipo(string? valor, int maximo)
    {
        var limpio = TextoMobiControl.Normalizar(valor);
        if (limpio is null) return null;

        var clave = new string(limpio.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return clave.Length == 0 ? null : clave.Length <= maximo ? clave : clave[..maximo];
    }

    /// <summary>La solicitud con el acta y todo lo que hace falta para mostrarla.</summary>
    private async Task<SolicitudFirma?> BuscarCompletaAsync(string idSolicitud, CancellationToken ct)
    {
        var idOrigen = TextoMobiControl.Normalizar(idSolicitud, 100)
            ?? throw new ErrorSolicitudException("Identificador de solicitud inválido.");

        return await db.Solicitudes
            .Include(s => s.Entrega).ThenInclude(e => e!.Empleado)
            .Include(s => s.Entrega).ThenInclude(e => e!.Dispositivo)
            .Include(s => s.Entrega).ThenInclude(e => e!.Estado)
            .Include(s => s.Entrega).ThenInclude(e => e!.Canal)
            .Include(s => s.Entrega).ThenInclude(e => e!.Distrito)
            .FirstOrDefaultAsync(s => s.IdSolicitudOrigen == idOrigen, ct);
    }

    private async Task<SolicitudFirma?> BuscarPorOrigenAsync(string idSolicitud, CancellationToken ct)
    {
        var idOrigen = TextoMobiControl.Normalizar(idSolicitud, 100)
            ?? throw new ErrorSolicitudException("Identificador de solicitud inválido.");

        return await db.Solicitudes
            .Include(s => s.Entrega)
            .FirstOrDefaultAsync(s => s.IdSolicitudOrigen == idOrigen, ct);
    }

    private SolicitudCreadaResponse Creada(SolicitudFirma solicitud, bool duplicada) =>
        new(solicitud.SolicitudUid,
            solicitud.IdSolicitudOrigen,
            EstadoVisible(solicitud),
            enlaces.UrlParaFirmar(solicitud.SolicitudUid),
            Utc(solicitud.FechaVencimiento),
            duplicada);

    private SolicitudDto AVista(SolicitudFirma solicitud) =>
        new(solicitud.IdSolicitudOrigen,
            solicitud.SolicitudUid,
            EstadoVisible(solicitud),
            Utc(solicitud.FechaCreacion),
            Utc(solicitud.FechaActualizacion),
            Utc(solicitud.FechaVencimiento),
            Utc(solicitud.FechaFirma),
            Utc(solicitud.FechaRechazo),
            solicitud.MotivoRechazo,
            solicitud.RechazadoPor,
            solicitud.Entrega is not null ? enlaces.UrlDocumento(solicitud.SolicitudUid) : null,
            LeerDatos(solicitud),
            solicitud.Entrega is { Empleado: not null, Dispositivo: not null } entrega ? ActaFirmadaDto.Desde(entrega) : null,
            solicitud.Entrega?.EntregaUid,
            solicitud.EstadoCallback?.ToString(),
            solicitud.IntentosCallback,
            solicitud.CodigoHttpCallback,
            solicitud.UltimoErrorCallback,
            Utc(solicitud.FechaCallback));

    private static string EstadoVisible(SolicitudFirma solicitud) =>
        solicitud.Estado == EstadoSolicitud.PENDIENTE && solicitud.FechaVencimiento <= DateTime.UtcNow
            ? EstadoVencida
            : solicitud.Estado.ToString();

    /// <summary>
    /// Todas las fechas se guardan en UTC, pero la base las devuelve sin marcar. Sin la marca
    /// salen sin la Z y quien las lee —el origen, el navegador— las toma como hora local.
    /// </summary>
    private static DateTime Utc(DateTime fecha) => DateTime.SpecifyKind(fecha, DateTimeKind.Utc);

    private static DateTime? Utc(DateTime? fecha) => fecha is { } f ? Utc(f) : null;

    private static DatosSolicitud LeerDatos(SolicitudFirma solicitud) =>
        JsonSerializer.Deserialize<DatosSolicitud>(solicitud.DatosOrigen, Json)
        ?? throw new InvalidOperationException($"La solicitud {solicitud.SolicitudUid} no tiene datos legibles.");
}
