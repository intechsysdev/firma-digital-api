using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobiControlFirma.API.Configuration;
using MobiControlFirma.Application.Catalogos;
using MobiControlFirma.Application.Common.Interfaces;

namespace MobiControlFirma.API.Controllers;

/// <summary>
/// Catálogos de apoyo. Se llenan solos con lo que reportan los equipos, y este endpoint sirve
/// para que un formulario o un reporte pueda ofrecerlos como lista.
/// </summary>
[ApiController]
[Route("api/v1/catalogos")]
[ApiKey(RolApi.Dispositivo)]
public class CatalogosController(IApplicationDbContext db) : ControllerBase
{
    [HttpGet("distritos")]
    public async Task<ActionResult<IReadOnlyList<CatalogoItemDto>>> Distritos(CancellationToken ct) =>
        Ok(await db.Distritos.AsNoTracking()
            .Where(d => d.Activo)
            .OrderBy(d => d.Nombre)
            .Select(d => new CatalogoItemDto(d.DistritoId, d.Nombre))
            .ToListAsync(ct));

    [HttpGet("canales")]
    public async Task<ActionResult<IReadOnlyList<CatalogoItemDto>>> Canales(CancellationToken ct) =>
        Ok(await db.Canales.AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new CatalogoItemDto(c.CanalId, c.Nombre))
            .ToListAsync(ct));

    [HttpGet("estados")]
    public async Task<ActionResult<IReadOnlyList<CatalogoItemDto>>> Estados(CancellationToken ct) =>
        Ok(await db.EstadosDispositivo.AsNoTracking()
            .OrderBy(e => e.Nombre)
            .Select(e => new CatalogoItemDto(e.EstadoId, e.Nombre))
            .ToListAsync(ct));
}
