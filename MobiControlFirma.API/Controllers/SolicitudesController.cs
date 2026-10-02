using Microsoft.AspNetCore.Mvc;
using MobiControlFirma.API.Configuration;
using MobiControlFirma.Application.Entregas;
using MobiControlFirma.Application.Solicitudes;

namespace MobiControlFirma.API.Controllers;

/// <summary>
/// Solicitudes de firma por enlace. Las crea el sistema de origen con una credencial de One de la
/// empresa para esta app (X-Api-Key + X-Api-Secret); también se aceptan la llave de los equipos o
/// un usuario de la consola con una empresa elegida. La guía de uso está publicada en One, en la
/// pestaña "Cómo integrar" de la app en cada empresa.
/// </summary>
[ApiController]
[Route("api/v1/solicitudes")]
[ApiKey(RolApi.Dispositivo)]
public class SolicitudesController(IServicioSolicitudes solicitudes) : ControllerBase
{
    /// <summary>
    /// Guarda los datos precargados y devuelve el enlace de firma. Repetir el mismo
    /// <c>idSolicitud</c> devuelve la solicitud existente con un enlace nuevo que sirve igual.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SolicitudCreadaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(SolicitudCreadaResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SolicitudCreadaResponse>> Crear(
        CrearSolicitudRequest solicitud, CancellationToken ct)
    {
        var resultado = await solicitudes.CrearAsync(solicitud, ct);

        return resultado.Duplicada
            ? Ok(resultado)
            : CreatedAtAction(nameof(Obtener), new { idSolicitud = resultado.IdSolicitud }, resultado);
    }

    /// <summary>
    /// Solicitudes que cambiaron (creadas, corregidas, firmadas o rechazadas) desde una fecha, de
    /// la más vieja a la más nueva. Para sincronizar sin recibir avisos: guarde la
    /// <c>fechaActualizacion</c> de la última que procesó y úsela como <c>desde</c> la próxima vez.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginaDto<SolicitudResumenDto>>> Listar(
        [FromQuery] DateTimeOffset? desde,
        [FromQuery] string? estado,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 100,
        CancellationToken ct = default) =>
        Ok(await solicitudes.ListarAsync(desde, estado, pagina, tamanoPagina, ct));

    /// <summary>
    /// La solicitud pendiente de un equipo, por IMEI o serial. La usa el formulario del equipo
    /// para completar lo que MobiControl no tiene; 404 si el origen no mandó ninguna.
    /// </summary>
    [HttpGet("pendiente")]
    public async Task<ActionResult<PrecargaEquipoDto>> Pendiente(
        [FromQuery] string? imei, [FromQuery] string? serial, CancellationToken ct)
    {
        var precarga = await solicitudes.BuscarPrecargaAsync(imei, serial, ct);
        return precarga is null
            ? NotFound(new { message = "No hay una solicitud pendiente para este equipo." })
            : Ok(precarga);
    }

    /// <summary>
    /// Todo de la solicitud por el identificador del origen: los datos que se mandaron, el estado
    /// y, si se firmó, el acta con lo que se firmó y la URL del PDF.
    /// </summary>
    [HttpGet("{idSolicitud}")]
    public async Task<ActionResult<SolicitudDto>> Obtener(string idSolicitud, CancellationToken ct)
    {
        var solicitud = await solicitudes.ConsultarAsync(idSolicitud, ct);
        return solicitud is null
            ? NotFound(new { message = "No existe una solicitud con ese identificador." })
            : Ok(solicitud);
    }

    /// <summary>El acta firmada en PDF. 404 mientras no se haya firmado.</summary>
    [HttpGet("{idSolicitud}/pdf")]
    public async Task<IActionResult> DescargarPdf(string idSolicitud, CancellationToken ct)
    {
        var archivo = await solicitudes.DescargarPdfPorOrigenAsync(idSolicitud, ct);
        if (archivo is null) return NotFound(new { message = "La solicitud no existe o todavía no está firmada." });

        Response.Headers.ContentDisposition = $"inline; filename=\"{archivo.NombreArchivo}\"";
        return File(archivo.Contenido, archivo.TipoContenido);
    }

    /// <summary>
    /// Vuelve a encolar el callback. Sirve cuando se agotaron los reintentos, casi siempre
    /// porque la URL del callback no estaba configurada en One cuando se firmó.
    /// </summary>
    [HttpPost("{idSolicitud}/reintentar-callback")]
    public async Task<ActionResult<SolicitudDto>> ReintentarCallback(string idSolicitud, CancellationToken ct) =>
        Ok(await solicitudes.ReintentarCallbackAsync(idSolicitud, ct));
}
