using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MobiControlFirma.API.Configuration;
using MobiControlFirma.Application.Solicitudes;

namespace MobiControlFirma.API.Controllers;

/// <summary>
/// Lo que usa el formulario web de firma. No pide sesión ni llave: el token del enlace es la
/// credencial, y el filtro lo traduce a la solicitud y a su empresa antes de llegar aquí.
/// </summary>
[ApiController]
[Route("api/v1/firmas/{" + EnlaceFirmaAttribute.ParametroRuta + "}")]
[EnlaceFirma]
public class FirmasController(IServicioSolicitudes solicitudes) : ControllerBase
{
    private Guid SolicitudUid => (Guid)HttpContext.Items[EnlaceFirmaAttribute.ClaveSolicitud]!;

    /// <summary>Datos precargados y estado de la solicitud.</summary>
    [HttpGet]
    public async Task<ActionResult<FormularioFirmaDto>> Obtener(CancellationToken ct) =>
        Ok(await solicitudes.ObtenerFormularioAsync(SolicitudUid, ct));

    /// <summary>Registra el acta con los datos revisados y la firma.</summary>
    [HttpPost]
    // Cuesta lo mismo que una firma desde el equipo: un PDF y las llamadas a MobiControl.
    [EnableRateLimiting(PoliticasLimite.Firmas)]
    public async Task<ActionResult<FirmaRegistradaResponse>> Firmar(
        FirmarSolicitudRequest firma, CancellationToken ct) =>
        Ok(await solicitudes.FirmarAsync(
            SolicitudUid,
            firma,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            ct));

    /// <summary>El acta firmada, para que el asociado la vea al terminar.</summary>
    [HttpGet("pdf")]
    public async Task<IActionResult> DescargarPdf(CancellationToken ct)
    {
        var archivo = await solicitudes.DescargarPdfAsync(SolicitudUid, ct);
        if (archivo is null) return NotFound(new { message = "El acta todavía no está disponible." });

        Response.Headers.ContentDisposition = $"inline; filename=\"{archivo.NombreArchivo}\"";
        return File(archivo.Contenido, archivo.TipoContenido);
    }
}
