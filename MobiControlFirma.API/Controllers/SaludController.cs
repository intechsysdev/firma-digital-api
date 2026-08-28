using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.API.Controllers;

/// <summary>
/// Comprobación de vida. Va sin llave a propósito: el formulario la consulta antes de enviar
/// para distinguir "no hay red" de "el API respondió mal", y así saber si vale la pena
/// encolar el acta y reintentar.
/// </summary>
[ApiController]
[Route("api/v1/salud")]
public class SaludController(ApplicationDbContext db, IClienteMobiControl mobiControl) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Estado(CancellationToken ct)
    {
        var baseDatos = await db.Database.CanConnectAsync(ct);

        return Ok(new
        {
            estado = baseDatos ? "ok" : "degradado",
            baseDatos,
            mobiControlConfigurado = mobiControl.EstaConfigurado,
            utc = DateTime.UtcNow,
        });
    }
}
