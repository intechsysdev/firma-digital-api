using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobiControlFirma.API.Configuration;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Domain.Entities;
using MobiControlFirma.Infrastructure.Identidad;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.API.Controllers;

public record VinculoDto(
    int EmpresaId, Guid OneTenantId, string OneSlug, string Nombre,
    bool OneConfigurado, string ApiKeyPrefijo, DateTime? ApiKeyRotadaEn,
    bool Activo, int Actas, DateTime FechaCreacion);

public record VinculoAltaRequest(
    Guid OneTenantId, string OneSlug, string Nombre, string? OneApiKey, string? OneApiSecret);

/// <summary>La llave de los equipos solo se ve al crear el vínculo o al rotarla.</summary>
public record VinculoCreadoResponse(VinculoDto Vinculo, string ApiKeyDispositivo);

/// <summary>Lo que One responde para este vínculo, sin secretos: sirve para comprobarlo.</summary>
public record ComprobacionDto(
    bool Alcanzable, string? TenantNombre, string? TenantSlug,
    bool MobiControlConfigurado, bool CorreoConfigurado,
    string? MobiControlBaseUrl, string? InfobipRemitente, string? CorreosCopia, string? ConfigVersion);

/// <summary>
/// Vínculos entre este sistema y los tenants de One. No son empresas: las empresas se crean y se
/// configuran en One. Aquí solo se registra a qué tenant pertenece cada compartimento de datos,
/// con qué credencial preguntarle su configuración, y qué llave llevan sus equipos.
///
/// La llave del dispositivo tiene que vivir aquí y no en One porque es lo que se usa para saber
/// de qué tenant es la petición que acaba de llegar: sin resolverla antes no hay a quién
/// preguntarle nada.
/// </summary>
[ApiController]
[Route("api/v1/vinculos")]
[Authorize(Roles = One.RolPlataforma)]
public class VinculosController(
    ApplicationDbContext db,
    IProveedorConfiguracion configuracion) : ControllerBase
{
    private static VinculoDto AVista(Empresa e, int actas) => new(
        e.EmpresaId, e.OneTenantId, e.OneSlug, e.Nombre,
        e.OneConfigurado, e.ApiKeyPrefijo, e.ApiKeyRotadaEn,
        e.Activo, actas, e.FechaCreacion);

    private Task<int> ContarActasAsync(int empresaId, CancellationToken ct) =>
        db.Entregas.IgnoreQueryFilters().CountAsync(e => e.EmpresaId == empresaId, ct);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VinculoDto>>> Listar(CancellationToken ct)
    {
        var vinculos = await db.Empresas.AsNoTracking().OrderBy(e => e.Nombre).ToListAsync(ct);

        var actas = await db.Entregas.IgnoreQueryFilters()
            .GroupBy(e => e.EmpresaId)
            .Select(g => new { EmpresaId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.EmpresaId, x => x.Total, ct);

        return Ok(vinculos.Select(e => AVista(e, actas.GetValueOrDefault(e.EmpresaId))).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<VinculoCreadoResponse>> Crear(
        [FromBody] VinculoAltaRequest solicitud, CancellationToken ct)
    {
        if (solicitud.OneTenantId == Guid.Empty)
            return BadRequest(new { message = "Hace falta el identificador del tenant en One." });

        if (await db.Empresas.AnyAsync(e => e.OneTenantId == solicitud.OneTenantId, ct))
            return BadRequest(new { message = "Ese tenant ya está vinculado." });

        var llave = LlavesDispositivo.Generar();

        var vinculo = new Empresa
        {
            OneTenantId = solicitud.OneTenantId,
            OneSlug = solicitud.OneSlug?.Trim() ?? string.Empty,
            Nombre = solicitud.Nombre?.Trim() ?? solicitud.OneSlug?.Trim() ?? "Sin nombre",
            OneApiKey = solicitud.OneApiKey?.Trim(),
            OneApiSecret = solicitud.OneApiSecret?.Trim(),
            ApiKeyHash = LlavesDispositivo.Resumir(llave),
            ApiKeyPrefijo = LlavesDispositivo.Prefijo(llave),
            ApiKeyRotadaEn = DateTime.UtcNow,
            FechaCreacion = DateTime.UtcNow,
        };

        db.Empresas.Add(vinculo);
        await db.SaveChangesAsync(ct);

        // Estados base, para que el tenant sirva desde su primera acta.
        await ApplicationDbContextSeed.SembrarEmpresaAsync(db, vinculo.EmpresaId, ct);

        return Ok(new VinculoCreadoResponse(AVista(vinculo, 0), llave));
    }

    [HttpPut("{empresaId:int}")]
    public async Task<ActionResult<VinculoDto>> Actualizar(
        int empresaId, [FromBody] VinculoAltaRequest solicitud, CancellationToken ct)
    {
        var vinculo = await db.Empresas.FirstOrDefaultAsync(e => e.EmpresaId == empresaId, ct);
        if (vinculo is null) return NotFound(new { message = "No existe ese vínculo." });

        if (!string.IsNullOrWhiteSpace(solicitud.Nombre)) vinculo.Nombre = solicitud.Nombre.Trim();
        if (!string.IsNullOrWhiteSpace(solicitud.OneSlug)) vinculo.OneSlug = solicitud.OneSlug.Trim();
        if (!string.IsNullOrWhiteSpace(solicitud.OneApiKey)) vinculo.OneApiKey = solicitud.OneApiKey.Trim();

        // El secreto no se devuelve nunca: si llegara vacío y se escribiera, editar el nombre
        // dejaría al vínculo sin poder consultar su configuración.
        if (!string.IsNullOrWhiteSpace(solicitud.OneApiSecret))
            vinculo.OneApiSecret = solicitud.OneApiSecret.Trim();

        vinculo.FechaActualizacion = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // La credencial pudo cambiar: lo cacheado ya no vale.
        configuracion.Olvidar(empresaId);

        return Ok(AVista(vinculo, await ContarActasAsync(empresaId, ct)));
    }

    /// <summary>
    /// Comprueba el vínculo contra One y devuelve lo que responde, sin secretos. Es la forma de
    /// saber si la credencial sirve y si el tenant tiene su configuración cargada, sin tener que
    /// firmar un acta de prueba.
    /// </summary>
    [HttpGet("{empresaId:int}/comprobacion")]
    public async Task<ActionResult<ComprobacionDto>> Comprobar(int empresaId, CancellationToken ct)
    {
        if (!await db.Empresas.AnyAsync(e => e.EmpresaId == empresaId, ct))
            return NotFound(new { message = "No existe ese vínculo." });

        configuracion.Olvidar(empresaId);
        var config = await configuracion.ObtenerAsync(empresaId, ct);

        if (config is null)
            return Ok(new ComprobacionDto(false, null, null, false, false, null, null, null, null));

        return Ok(new ComprobacionDto(
            true, config.TenantNombre, config.TenantSlug,
            config.MobiControlConfigurado, config.CorreoConfigurado,
            config.MobiControlBaseUrl, config.InfobipRemitente, config.CorreosCopia, config.ConfigVersion));
    }

    /// <summary>
    /// Genera una llave nueva para los equipos. La anterior deja de servir en el acto, así que
    /// hay que reinstalar el formulario en la flota después de rotarla.
    /// </summary>
    [HttpPost("{empresaId:int}/rotar-llave")]
    public async Task<ActionResult<VinculoCreadoResponse>> RotarLlave(int empresaId, CancellationToken ct)
    {
        var vinculo = await db.Empresas.FirstOrDefaultAsync(e => e.EmpresaId == empresaId, ct);
        if (vinculo is null) return NotFound(new { message = "No existe ese vínculo." });

        var llave = LlavesDispositivo.Generar();
        vinculo.ApiKeyHash = LlavesDispositivo.Resumir(llave);
        vinculo.ApiKeyPrefijo = LlavesDispositivo.Prefijo(llave);
        vinculo.ApiKeyRotadaEn = DateTime.UtcNow;
        vinculo.FechaActualizacion = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new VinculoCreadoResponse(AVista(vinculo, await ContarActasAsync(empresaId, ct)), llave));
    }

    /// <summary>
    /// Activa o desactiva el vínculo. No se borra: sus actas son evidencia de entregas. Uno
    /// desactivado deja de aceptar actas nuevas y conserva su histórico.
    /// </summary>
    [HttpPost("{empresaId:int}/activo")]
    public async Task<IActionResult> CambiarEstado(int empresaId, [FromBody] bool activo, CancellationToken ct)
    {
        var vinculo = await db.Empresas.FirstOrDefaultAsync(e => e.EmpresaId == empresaId, ct);
        if (vinculo is null) return NotFound(new { message = "No existe ese vínculo." });

        vinculo.Activo = activo;
        vinculo.FechaActualizacion = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { message = activo ? "Vínculo activado." : "Vínculo desactivado." });
    }
}
