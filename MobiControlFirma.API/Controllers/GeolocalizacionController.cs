using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobiControlFirma.Application.Common;
using MobiControlFirma.Application.Geolocalizacion;

namespace MobiControlFirma.API.Controllers;

/// <summary>
/// Dónde están los equipos de la empresa y quién los tiene. Solo para la consola: lo consulta
/// un usuario con sesión de One y empresa elegida, nunca un equipo con su llave.
/// </summary>
[ApiController]
[Route("api/v1/geolocalizacion")]
[Authorize]
public class GeolocalizacionController(IServicioGeolocalizacion geolocalizacion) : ControllerBase
{
    /// <summary>
    /// Si la empresa tiene MobiControl y la key de Google Maps. La key sale al navegador porque
    /// el mapa se dibuja allá; por eso debe estar restringida por dominio en Google Cloud.
    /// </summary>
    [HttpGet("configuracion")]
    public async Task<ActionResult<ConfiguracionGeoDto>> Configuracion(CancellationToken ct) =>
        Ok(await geolocalizacion.ConfiguracionAsync(ct));

    /// <summary>Toda la flota: estado en MobiControl, última posición y a quién se le entregó.</summary>
    [HttpGet("dispositivos")]
    public async Task<ActionResult<FlotaDto>> Dispositivos(CancellationToken ct) =>
        Ok(await geolocalizacion.ListarAsync(ct));

    /// <summary>Puntos GPS de un día (hora de Colombia), en orden.</summary>
    [HttpGet("dispositivos/{deviceId}/recorrido")]
    public async Task<ActionResult<IReadOnlyList<PuntoRecorridoDto>>> Recorrido(
        string deviceId, [FromQuery] DateOnly? fecha, CancellationToken ct)
    {
        var dia = fecha ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Bogota"));

        if (dia > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            throw new ErrorSolicitudException("La fecha del recorrido no puede ser futura.");

        return Ok(await geolocalizacion.RecorridoAsync(deviceId, dia, ct));
    }

    /// <summary>
    /// Pide al equipo que reporte su posición. Despierta el teléfono y gasta batería, así que
    /// solo se hace cuando alguien lo pide, nunca al consultar la flota.
    /// </summary>
    [HttpPost("dispositivos/{deviceId}/localizar")]
    public async Task<IActionResult> Localizar(string deviceId, CancellationToken ct)
    {
        await geolocalizacion.LocalizarAsync(deviceId, ct);
        return Accepted(new { message = "Se le pidió al equipo su ubicación. Llega en unos segundos si está en línea." });
    }
}
