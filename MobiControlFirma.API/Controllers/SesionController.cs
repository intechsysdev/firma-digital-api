using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobiControlFirma.API.Configuration;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.API.Controllers;

public record EmpresaAccesibleDto(int EmpresaId, Guid OneTenantId, string Slug, string Nombre, string? Rol);

public record SesionDto(
    string Correo, string? Nombre, bool EsAdministradorPlataforma,
    IReadOnlyList<EmpresaAccesibleDto> Empresas);

/// <summary>
/// Quién es quien llama y a qué empresas alcanza. La consola lo consulta al entrar: el token de
/// One trae los identificadores de las empresas del usuario, pero no sus nombres ni cuáles de
/// ellas están vinculadas a este sistema.
/// </summary>
[ApiController]
[Route("api/v1/sesion")]
[Authorize]
public class SesionController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SesionDto>> Yo(CancellationToken ct)
    {
        var esPlataforma = User.IsInRole(One.RolPlataforma);
        var pertenencias = ResolucionTenantMiddleware.Pertenencias(User);

        // Un administrador de plataforma alcanza todas las empresas vinculadas; el resto, solo
        // aquellas en las que One dice que es miembro.
        var consulta = db.Empresas.AsNoTracking().Where(e => e.Activo);

        if (!esPlataforma)
            consulta = consulta.Where(e => pertenencias.Contains(e.OneTenantId));

        var empresas = await consulta
            .OrderBy(e => e.Nombre)
            .Select(e => new { e.EmpresaId, e.OneTenantId, e.OneSlug, e.Nombre })
            .ToListAsync(ct);

        var salida = empresas
            .Select(e => new EmpresaAccesibleDto(
                e.EmpresaId, e.OneTenantId, e.OneSlug, e.Nombre,
                ResolucionTenantMiddleware.RolEn(User, e.OneTenantId)))
            .ToList();

        return Ok(new SesionDto(
            User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? User.Identity?.Name ?? "",
            User.FindFirstValue(JwtRegisteredClaimNames.Name),
            esPlataforma,
            salida));
    }
}
