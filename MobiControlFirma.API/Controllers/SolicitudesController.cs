using Microsoft.AspNetCore.Mvc;
using MobiControlFirma.API.Configuration;
using MobiControlFirma.Application.Solicitudes;

namespace MobiControlFirma.API.Controllers;

/// <summary>
/// Solicitudes de firma por enlace. Las crea el sistema de origen con la llave de la empresa
/// —la misma de los equipos, que es la que dice de qué empresa es la petición— o un usuario de
/// la consola con una empresa elegida.
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

    /// <summary>Estado de la solicitud y de su callback, por el identificador del origen.</summary>
    [HttpGet("{idSolicitud}")]
    public async Task<ActionResult<SolicitudDto>> Obtener(string idSolicitud, CancellationToken ct)
    {
        var solicitud = await solicitudes.ConsultarAsync(idSolicitud, ct);
        return solicitud is null
            ? NotFound(new { message = "No existe una solicitud con ese identificador." })
            : Ok(solicitud);
    }

    /// <summary>
    /// Vuelve a encolar el callback. Sirve cuando se agotaron los reintentos, casi siempre
    /// porque la URL del callback no estaba configurada en One cuando se firmó.
    /// </summary>
    [HttpPost("{idSolicitud}/reintentar-callback")]
    public async Task<ActionResult<SolicitudDto>> ReintentarCallback(string idSolicitud, CancellationToken ct) =>
        Ok(await solicitudes.ReintentarCallbackAsync(idSolicitud, ct));
}
