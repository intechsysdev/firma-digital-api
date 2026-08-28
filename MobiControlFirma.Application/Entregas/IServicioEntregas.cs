namespace MobiControlFirma.Application.Entregas;

/// <summary>Archivo recuperado del almacenamiento, listo para devolver por HTTP.</summary>
public record ArchivoDescargado(byte[] Contenido, string TipoContenido, string NombreArchivo);

/// <summary>Casos de uso de las actas de entrega.</summary>
public interface IServicioEntregas
{
    /// <summary>
    /// Registra el acta: guarda la firma, genera el PDF y sincroniza con MobiControl.
    /// Si la clave de idempotencia ya existe, devuelve el acta anterior en vez de duplicarla.
    /// </summary>
    Task<EntregaCreadaResponse> RegistrarAsync(
        RegistrarEntregaRequest solicitud, string? ipOrigen, string? userAgent, CancellationToken ct = default);

    /// <summary>Última acta firmada de un equipo, para avisar antes de que alguien firme de nuevo.</summary>
    Task<EstadoFirmaDispositivoDto> ConsultarEstadoDispositivoAsync(string deviceId, CancellationToken ct = default);

    /// <summary>Listado de actas con filtros básicos, para el reporte administrativo.</summary>
    Task<PaginaDto<EntregaResumenDto>> ListarAsync(
        string? busqueda, DateOnly? desde, DateOnly? hasta, string? estadoProceso,
        int pagina, int tamanoPagina, CancellationToken ct = default);

    Task<EntregaResumenDto?> ObtenerAsync(Guid entregaUid, CancellationToken ct = default);

    Task<ArchivoDescargado?> DescargarPdfAsync(Guid entregaUid, CancellationToken ct = default);

    Task<ArchivoDescargado?> DescargarFirmaAsync(Guid entregaUid, CancellationToken ct = default);

    /// <summary>
    /// Vuelve a intentar la sincronización de un acta que quedó en ERROR_SINCRONIZACION.
    /// La firma ya está a salvo; esto solo repara lo que MobiControl no alcanzó a recibir.
    /// </summary>
    Task<IReadOnlyList<ResultadoSincronizacionDto>> ReintentarSincronizacionAsync(
        Guid entregaUid, CancellationToken ct = default);
}
