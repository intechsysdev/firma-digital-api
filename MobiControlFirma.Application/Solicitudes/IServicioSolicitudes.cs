using MobiControlFirma.Application.Entregas;

namespace MobiControlFirma.Application.Solicitudes;

/// <summary>Firma por enlace: pedirla, mostrarla, firmarla y avisar al origen.</summary>
public interface IServicioSolicitudes
{
    /// <summary>
    /// Guarda los datos precargados y devuelve el enlace. Si el origen ya había pedido la misma
    /// solicitud, devuelve la existente en vez de abrir otra.
    /// </summary>
    Task<SolicitudCreadaResponse> CrearAsync(CrearSolicitudRequest solicitud, CancellationToken ct = default);

    /// <summary>Null si la empresa no tiene una solicitud con ese identificador de origen.</summary>
    Task<SolicitudDto?> ConsultarAsync(string idSolicitud, CancellationToken ct = default);

    /// <summary>
    /// Solicitudes que cambiaron desde una fecha, de la más vieja a la más nueva. Es como el origen
    /// se entera de las firmas cuando no recibe avisos.
    /// </summary>
    Task<PaginaDto<SolicitudResumenDto>> ListarAsync(
        DateTimeOffset? desde, string? estado, int pagina, int tamanoPagina, CancellationToken ct = default);

    /// <summary>
    /// Solicitud pendiente de un equipo, por IMEI o serial: lo que el formulario del equipo usa
    /// para completar lo que MobiControl no tiene. Null si el origen no ha mandado ninguna.
    /// </summary>
    Task<PrecargaEquipoDto?> BuscarPrecargaAsync(string? imei, string? serial, CancellationToken ct = default);

    /// <summary>
    /// Registra el acta firmada en el equipo y la ata a la solicitud de la que salieron sus datos.
    /// El acta se registra aunque la solicitud ya no esté pendiente; solo que entonces no se ata.
    /// </summary>
    Task<EntregaCreadaResponse> RegistrarDesdeEquipoAsync(
        RegistrarEntregaRequest acta, string? ipOrigen, string? userAgent, CancellationToken ct = default);

    /// <summary>PDF del acta de una solicitud, por el identificador del origen. Null si no está firmada.</summary>
    Task<ArchivoDescargado?> DescargarPdfPorOrigenAsync(string idSolicitud, CancellationToken ct = default);

    /// <summary>Vuelve a poner en cola el aviso al origen, reiniciando los intentos.</summary>
    Task<SolicitudDto> ReintentarCallbackAsync(string idSolicitud, CancellationToken ct = default);

    Task<FormularioFirmaDto> ObtenerFormularioAsync(Guid solicitudUid, CancellationToken ct = default);

    /// <summary>
    /// Registra el acta con los datos revisados y deja el aviso al origen en la bandeja. Firmar
    /// dos veces la misma solicitud devuelve el acta de la primera.
    /// </summary>
    Task<FirmaRegistradaResponse> FirmarAsync(
        Guid solicitudUid, FirmarSolicitudRequest firma, string? ipOrigen, string? userAgent,
        CancellationToken ct = default);

    /// <summary>
    /// El asociado no acepta el acta. Es definitivo, y se avisa al origen como una firma. Rechazar
    /// dos veces devuelve el primer rechazo.
    /// </summary>
    Task<RechazoRegistradoResponse> RechazarAsync(
        Guid solicitudUid, RechazarSolicitudRequest rechazo, CancellationToken ct = default);

    Task<ArchivoDescargado?> DescargarPdfAsync(Guid solicitudUid, CancellationToken ct = default);
}
