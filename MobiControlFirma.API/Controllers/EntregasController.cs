using Microsoft.AspNetCore.Mvc;
using MobiControlFirma.API.Configuration;
using MobiControlFirma.Application.Entregas;

namespace MobiControlFirma.API.Controllers;

/// <summary>Actas de entrega firmadas desde el dispositivo.</summary>
[ApiController]
[Route("api/v1/entregas")]
public class EntregasController(IServicioEntregas entregas) : ControllerBase
{
    /// <summary>
    /// Registra el acta firmada: guarda la firma, genera el PDF y marca el equipo en MobiControl.
    /// </summary>
    [HttpPost]
    [ApiKey(RolApi.Dispositivo)]
    [ProducesResponseType(typeof(EntregaCreadaResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<EntregaCreadaResponse>> Registrar(
        RegistrarEntregaRequest solicitud, CancellationToken ct)
    {
        var resultado = await entregas.RegistrarAsync(
            solicitud,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            ct);

        // El reenvío de un acta que ya existía devuelve 200: para el formulario es un éxito,
        // pero no se creó nada nuevo.
        return resultado.Duplicada
            ? Ok(resultado)
            : CreatedAtAction(nameof(Obtener), new { entregaUid = resultado.EntregaUid }, resultado);
    }

    /// <summary>
    /// Dice si un equipo ya tiene acta firmada. El formulario lo consulta al abrir para avisarlo
    /// antes de que el asociado vuelva a firmar sin saberlo.
    /// </summary>
    [HttpGet("dispositivo/{deviceId}")]
    [ApiKey(RolApi.Dispositivo)]
    public async Task<ActionResult<EstadoFirmaDispositivoDto>> EstadoDispositivo(
        string deviceId, CancellationToken ct) =>
        Ok(await entregas.ConsultarEstadoDispositivoAsync(deviceId, ct));

    /// <summary>Histórico de actas con filtros básicos.</summary>
    [HttpGet]
    [ApiKey(RolApi.Administrador)]
    public async Task<ActionResult<PaginaDto<EntregaResumenDto>>> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] string? estadoProceso,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 25,
        CancellationToken ct = default) =>
        Ok(await entregas.ListarAsync(busqueda, desde, hasta, estadoProceso, pagina, tamanoPagina, ct));

    /// <summary>Detalle de un acta.</summary>
    [HttpGet("{entregaUid:guid}")]
    [ApiKey(RolApi.Administrador)]
    public async Task<ActionResult<EntregaResumenDto>> Obtener(Guid entregaUid, CancellationToken ct)
    {
        var entrega = await entregas.ObtenerAsync(entregaUid, ct);
        return entrega is null ? NotFound(new { message = "No existe un acta con ese identificador." }) : Ok(entrega);
    }

    /// <summary>Acta en PDF, generada en el servidor al momento de firmar.</summary>
    [HttpGet("{entregaUid:guid}/pdf")]
    [ApiKey(RolApi.Dispositivo)]
    public async Task<IActionResult> DescargarPdf(Guid entregaUid, CancellationToken ct)
    {
        var archivo = await entregas.DescargarPdfAsync(entregaUid, ct);
        if (archivo is null) return NotFound(new { message = "El acta no está disponible." });

        // Inline y no attachment: en el dispositivo el asociado quiere verla, no descargarla.
        Response.Headers.ContentDisposition = $"inline; filename=\"{archivo.NombreArchivo}\"";
        return File(archivo.Contenido, archivo.TipoContenido);
    }

    /// <summary>Imagen de la firma tal como se capturó.</summary>
    [HttpGet("{entregaUid:guid}/firma")]
    [ApiKey(RolApi.Dispositivo)]
    public async Task<IActionResult> DescargarFirma(Guid entregaUid, CancellationToken ct)
    {
        var archivo = await entregas.DescargarFirmaAsync(entregaUid, ct);
        return archivo is null
            ? NotFound(new { message = "La firma no está disponible." })
            : File(archivo.Contenido, archivo.TipoContenido);
    }

    /// <summary>
    /// Reintenta la sincronización de un acta que quedó en ERROR_SINCRONIZACION. Sirve para
    /// reparar el rezago cuando la consola de MobiControl estuvo caída.
    /// </summary>
    [HttpPost("{entregaUid:guid}/reintentar-sincronizacion")]
    [ApiKey(RolApi.Administrador)]
    public async Task<ActionResult<IReadOnlyList<ResultadoSincronizacionDto>>> Reintentar(
        Guid entregaUid, CancellationToken ct) =>
        Ok(await entregas.ReintentarSincronizacionAsync(entregaUid, ct));

    /// <summary>
    /// Cierra la entrega desde el equipo: marca el atributo de firma en MobiControl, que es lo
    /// que hace desaparecer el formulario y devuelve el dispositivo al asociado.
    ///
    /// Va separada del registro a propósito. Si el acta se sincronizara al guardarla, MobiControl
    /// cerraría el formulario en ese mismo instante y el acta en PDF se perdería de vista antes
    /// de que nadie alcance a abrirla. Registrar primero y cerrar después deja al asociado
    /// revisar el documento y terminar cuando quiera.
    /// </summary>
    [HttpPost("{entregaUid:guid}/finalizar")]
    [ApiKey(RolApi.Dispositivo)]
    public async Task<ActionResult<IReadOnlyList<ResultadoSincronizacionDto>>> Finalizar(
        Guid entregaUid, CancellationToken ct) =>
        Ok(await entregas.ReintentarSincronizacionAsync(entregaUid, ct));
}
