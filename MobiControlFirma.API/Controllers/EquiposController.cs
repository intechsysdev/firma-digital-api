using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobiControlFirma.API.Configuration;
using MobiControlFirma.Application.Equipos;

namespace MobiControlFirma.API.Controllers;

/// <summary>
/// Los equipos de MobiControl de la empresa y las actas que se crean desde la consola. Cualquier
/// miembro de la empresa puede crear actas; corregir los atributos del equipo en MobiControl
/// sigue siendo del dueño o administrador.
/// </summary>
[ApiController]
[Route("api/v1/equipos")]
[Authorize]
public class EquiposController(IServicioEquipos equipos, PermisosEmpresa permisos) : ControllerBase
{
    /// <summary>Los equipos de la consola con su última acta y el enlace que esté esperando firma.</summary>
    [HttpGet]
    public async Task<ActionResult<ListaEquiposDto>> Listar(CancellationToken ct) =>
        Ok(await equipos.ListarAsync(ct));

    /// <summary>Lo que se precarga en el acta: los datos del equipo y sus atributos en MobiControl.</summary>
    [HttpGet("{deviceId}/precarga")]
    public async Task<ActionResult<PrecargaActaDto>> Precarga(string deviceId, CancellationToken ct) =>
        Ok(await equipos.PrecargaAsync(deviceId, await permisos.PuedeAdministrarAsync(User, ct), ct));

    /// <summary>
    /// Crea el enlace de firma del acta. Con él se firma en esta misma pantalla o lo firma el
    /// asociado desde su celular; si se pide, se le manda por correo.
    /// </summary>
    [HttpPost("{deviceId}/actas")]
    [ProducesResponseType(typeof(ActaConsolaCreadaDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ActaConsolaCreadaDto>> CrearActa(
        string deviceId, CrearActaConsolaRequest solicitud, CancellationToken ct)
    {
        var creada = await equipos.CrearActaAsync(
            deviceId, solicitud, User.Identity?.Name, await permisos.PuedeAdministrarAsync(User, ct), ct);
        return StatusCode(StatusCodes.Status201Created, creada);
    }
}
