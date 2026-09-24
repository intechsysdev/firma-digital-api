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

    Task<ArchivoDescargado?> DescargarPdfAsync(Guid solicitudUid, CancellationToken ct = default);
}
