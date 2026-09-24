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
        var deviceId = TextoMobiControl.Normalizar(solicitud.DeviceId, 100)
            ?? throw new ErrorSolicitudException("Falta el identificador del equipo en MobiControl (deviceId).");

        var cedula = TextoMobiControl.Normalizar(solicitud.Cedula, 20)
            ?? throw new ErrorSolicitudException("Falta la cédula del asociado.");

        var existente = await db.Solicitudes
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdSolicitudOrigen == idOrigen, ct);

        if (existente is not null)
        {
            logger.LogInformation(
                "El origen volvió a pedir la solicitud {Origen}; se devuelve la existente {Uid}.",
                idOrigen, existente.SolicitudUid);
            return Creada(existente, duplicada: true);
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
            Fabricante: TextoMobiControl.Normalizar(solicitud.Fabricante, 100),
            Modelo: TextoMobiControl.Normalizar(solicitud.Modelo, 100),
            Imei: TextoMobiControl.Normalizar(solicitud.Imei, 50),
            Iccid: TextoMobiControl.Normalizar(solicitud.Iccid, 50),
            NumeroCelular: TextoMobiControl.Normalizar(solicitud.NumeroCelular, 30),
            Estado: TextoMobiControl.Normalizar(solicitud.Estado, 50),
            Canal: TextoMobiControl.Normalizar(solicitud.Canal, 100),
            Distrito: TextoMobiControl.Normalizar(solicitud.Distrito, 100),
            Costo: TextoMobiControl.Normalizar(solicitud.Costo, 50),
            Entregables: TextoMobiControl.Normalizar(solicitud.Entregables),
            CiudadFirma: ciudad);

        var ahora = DateTime.UtcNow;

        var nueva = new SolicitudFirma
        {
            EmpresaId = empresaId,
            SolicitudUid = Guid.NewGuid(),
            IdSolicitudOrigen = idOrigen,
            DatosOrigen = JsonSerializer.Serialize(datos, Json),
            Estado = EstadoSolicitud.PENDIENTE,
            FechaCreacion = ahora,
            FechaVencimiento = ahora.AddDays(solicitud.VigenciaDias ?? VigenciaPorDefectoDias),
        };

        db.Solicitudes.Add(nueva);
        await db.SaveChangesAsync(ct);

        // El envío del enlace por correo todavía no está: por ahora el origen recibe la URL en
        // la respuesta y es quien se la hace llegar al asociado.
        return Creada(nueva, duplicada: false);
    }

    public async Task<SolicitudDto?> ConsultarAsync(string idSolicitud, CancellationToken ct = default)
    {
        var solicitud = await BuscarPorOrigenAsync(idSolicitud, ct);
        return solicitud is null ? null : AVista(solicitud);
    }

    public async Task<SolicitudDto> ReintentarCallbackAsync(string idSolicitud, CancellationToken ct = default)
    {
        var solicitud = await BuscarPorOrigenAsync(idSolicitud, ct)
            ?? throw new ErrorSolicitudException("No existe una solicitud con ese identificador.");

        if (solicitud.Estado != EstadoSolicitud.FIRMADA)
            throw new ErrorSolicitudException("La solicitud todavía no se ha firmado: no hay nada que avisar.");

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
            solicitud.Entrega?.NombreAsociadoFirmante);
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

        if (solicitud.FechaVencimiento <= DateTime.UtcNow)
            throw new ErrorSolicitudException(
                "Este enlace de firma venció. Pide a quien te lo envió que genere uno nuevo.");

        var datos = LeerDatos(solicitud);

        var registro = await entregas.RegistrarAsync(new RegistrarEntregaRequest
        {
            // El equipo sale siempre de la solicitud: es lo único que el asociado no puede
            // cambiar, porque con él se marca la entrega en MobiControl.
            DeviceId = datos.DeviceId,
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
            FirmaBase64 = firma.FirmaBase64,
            // Atada a la solicitud y no al envío: aunque el formulario mande dos veces, o se
            // caiga el servidor entre guardar el acta y marcar la solicitud, sale una sola acta.
            ClaveIdempotencia = $"solicitud:{solicitud.SolicitudUid:N}",
            // Aquí no hay formulario en el equipo que esperar a que se cierre: la entrega se
            // marca en MobiControl en cuanto queda firmada.
            SincronizarMobiControl = true,
        }, ipOrigen, userAgent, ct);

        solicitud.Estado = EstadoSolicitud.FIRMADA;
        solicitud.EntregaId = registro.EntregaId;
        solicitud.FechaFirma = registro.FechaFirma;
        solicitud.EstadoCallback = EstadoCallback.PENDIENTE;
        solicitud.ProximoIntentoCallback = null;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Solicitud {Origen} firmada; acta {Acta}. El aviso al origen queda en la bandeja.",
            solicitud.IdSolicitudOrigen, registro.EntregaUid);

        return new FirmaRegistradaResponse(
            registro.EntregaUid, registro.FechaFirma, registro.EstadoProceso, registro.Duplicada);
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

    private static SolicitudDto AVista(SolicitudFirma solicitud) =>
        new(solicitud.SolicitudUid,
            solicitud.IdSolicitudOrigen,
            EstadoVisible(solicitud),
            Utc(solicitud.FechaCreacion),
            Utc(solicitud.FechaVencimiento),
            Utc(solicitud.FechaFirma),
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
