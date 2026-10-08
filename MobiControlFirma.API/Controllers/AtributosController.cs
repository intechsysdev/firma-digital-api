using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobiControlFirma.API.Configuration;
using MobiControlFirma.Application.Atributos;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.API.Controllers;

/// <summary>
/// Atributos personalizados de la consola de MobiControl de la empresa: sus definiciones, cuáles
/// escribe firma al firmar y sus valores en cada equipo. Todo va directo a MobiControl.
///
/// Leer lo puede cualquiera con sesión en la empresa. Cambiar la consola, solo su dueño o
/// administrador en One (o la plataforma): una definición vale para todos los equipos.
/// </summary>
[ApiController]
[Route("api/v1/atributos")]
[Authorize]
public class AtributosController(
    IServicioAtributos atributos,
    IContextoEmpresa empresa,
    ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ListaAtributosDto>> Listar(CancellationToken ct) =>
        Ok(await atributos.ListarAsync(await PuedeAdministrarAsync(ct), ct));

    [HttpPost]
    public async Task<ActionResult<AtributoDto>> Crear(GuardarAtributoRequest solicitud, CancellationToken ct)
    {
        if (!await PuedeAdministrarAsync(ct)) return Prohibido();
        var creado = await atributos.CrearAsync(solicitud, ct);
        return CreatedAtAction(nameof(Listar), null, creado);
    }

    /// <summary>Cambia nombre, tipo, opciones o si se pasa al equipo. El nombre va en la ruta, codificado.</summary>
    [HttpPut("{nombre}")]
    public async Task<ActionResult<AtributoDto>> Actualizar(string nombre, GuardarAtributoRequest solicitud, CancellationToken ct)
    {
        if (!await PuedeAdministrarAsync(ct)) return Prohibido();
        return Ok(await atributos.ActualizarAsync(nombre, solicitud, ct));
    }

    [HttpDelete("{nombre}")]
    public async Task<IActionResult> Eliminar(string nombre, CancellationToken ct)
    {
        if (!await PuedeAdministrarAsync(ct)) return Prohibido();
        await atributos.EliminarAsync(nombre, ct);
        return NoContent();
    }

    /// <summary>Los atributos que firma escribe al firmar un acta. Mandan sobre las variables de One.</summary>
    [HttpPut("elegidos")]
    public async Task<IActionResult> Elegir(ElegirAtributosRequest solicitud, CancellationToken ct)
    {
        if (!await PuedeAdministrarAsync(ct)) return Prohibido();
        await atributos.ElegirAsync(solicitud, ct);
        return NoContent();
    }

    [HttpGet("dispositivos/{deviceId}")]
    public async Task<ActionResult<ValoresEquipoDto>> Valores(string deviceId, CancellationToken ct) =>
        Ok(await atributos.ValoresAsync(deviceId, await PuedeAdministrarAsync(ct), ct));

    [HttpPut("dispositivos/{deviceId}")]
    public async Task<ActionResult<ValoresEquipoDto>> GuardarValores(
        string deviceId, GuardarValoresRequest solicitud, CancellationToken ct)
    {
        if (!await PuedeAdministrarAsync(ct)) return Prohibido();
        return Ok(await atributos.GuardarValoresAsync(deviceId, solicitud, ct));
    }

    // ------------------------------------------------------------------------------------

    private async Task<bool> PuedeAdministrarAsync(CancellationToken ct)
    {
        if (User.IsInRole(One.RolPlataforma)) return true;

        if (empresa.EmpresaId is not { } empresaId) return false;

        var tenantId = await db.Empresas.AsNoTracking()
            .Where(e => e.EmpresaId == empresaId)
            .Select(e => e.OneTenantId)
            .FirstAsync(ct);

        return ResolucionTenantMiddleware.RolEn(User, tenantId) is "Owner" or "Admin";
    }

    private ObjectResult Prohibido() => StatusCode(StatusCodes.Status403Forbidden, new
    {
        message = "Solo el dueño o un administrador de la empresa en Intechsys One puede cambiar los atributos de MobiControl.",
    });
}
