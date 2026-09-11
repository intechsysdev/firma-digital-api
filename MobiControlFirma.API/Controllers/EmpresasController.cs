using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobiControlFirma.Domain.Entities;
using MobiControlFirma.Infrastructure.Identidad;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.API.Controllers;

public record EmpresaDto(
    int EmpresaId, string Nombre, string? Nit, string CiudadFirma, bool Activo,
    string ApiKeyPrefijo, DateTime? ApiKeyRotadaEn,
    bool MobiControlConfigurado, string? MobiControlBaseUrl,
    string? MobiControlUsuario, string MobiControlAtributoFirma, string MobiControlAtributoFecha,
    int MobiControlTimeoutSegundos, int Actas, DateTime FechaCreacion);

public record EmpresaAltaRequest(
    string Nombre, string? Nit, string? CiudadFirma,
    string? MobiControlBaseUrl, string? MobiControlClientId, string? MobiControlClientSecret,
    string? MobiControlUsuario, string? MobiControlPassword,
    string? MobiControlAtributoFirma, string? MobiControlAtributoFecha, int? MobiControlTimeoutSegundos);

/// <summary>La llave solo se muestra al crearla o al rotarla: después ya no se puede recuperar.</summary>
public record EmpresaCreadaResponse(EmpresaDto Empresa, string ApiKeyDispositivo);

/// <summary>
/// Alta y mantenimiento de empresas. Territorio exclusivo del superadministrador: dar de alta
/// una empresa es crear un compartimento nuevo, y editarla toca las credenciales con las que
/// sus equipos hablan con MobiControl.
/// </summary>
[ApiController]
[Route("api/v1/empresas")]
[Authorize(Roles = Roles.SuperAdministrador)]
public class EmpresasController(ApplicationDbContext db) : ControllerBase
{
    private static EmpresaDto AVista(Empresa e, int actas) => new(
        e.EmpresaId, e.Nombre, e.Nit, e.CiudadFirma, e.Activo,
        e.ApiKeyPrefijo, e.ApiKeyRotadaEn,
        e.MobiControlConfigurado, e.MobiControlBaseUrl,
        e.MobiControlUsuario, e.MobiControlAtributoFirma, e.MobiControlAtributoFecha,
        e.MobiControlTimeoutSegundos, actas, e.FechaCreacion);

    // El superadministrador no pertenece a ninguna empresa, así que el filtro global dejaría
    // estos conteos en cero: aquí se pide explícitamente ver por encima de él.
    private Task<int> ContarActasAsync(int empresaId, CancellationToken ct) =>
        db.Entregas.IgnoreQueryFilters().CountAsync(e => e.EmpresaId == empresaId, ct);

    /// <summary>Empresas con su conteo de actas, para ver de un vistazo cuáles están en uso.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmpresaDto>>> Listar(CancellationToken ct)
    {
        var empresas = await db.Empresas.AsNoTracking().OrderBy(e => e.Nombre).ToListAsync(ct);

        var actas = await db.Entregas.IgnoreQueryFilters()
            .GroupBy(e => e.EmpresaId)
            .Select(g => new { EmpresaId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.EmpresaId, x => x.Total, ct);

        return Ok(empresas.Select(e => AVista(e, actas.GetValueOrDefault(e.EmpresaId))).ToList());
    }

    [HttpGet("{empresaId:int}")]
    public async Task<ActionResult<EmpresaDto>> Obtener(int empresaId, CancellationToken ct)
    {
        var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.EmpresaId == empresaId, ct);
        if (empresa is null) return NotFound(new { message = "No existe esa empresa." });

        return Ok(AVista(empresa, await ContarActasAsync(empresaId, ct)));
    }

    [HttpPost]
    public async Task<ActionResult<EmpresaCreadaResponse>> Crear(
        [FromBody] EmpresaAltaRequest solicitud, CancellationToken ct)
    {
        var nombre = solicitud.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest(new { message = "La empresa necesita un nombre." });

        if (await db.Empresas.AnyAsync(e => e.Nombre == nombre, ct))
            return BadRequest(new { message = $"Ya existe una empresa llamada {nombre}." });

        var llave = LlavesDispositivo.Generar();

        var empresa = new Empresa
        {
            Nombre = nombre,
            Nit = solicitud.Nit?.Trim(),
            CiudadFirma = string.IsNullOrWhiteSpace(solicitud.CiudadFirma) ? "Cali" : solicitud.CiudadFirma.Trim(),
            ApiKeyHash = LlavesDispositivo.Resumir(llave),
            ApiKeyPrefijo = LlavesDispositivo.Prefijo(llave),
            ApiKeyRotadaEn = DateTime.UtcNow,
            MobiControlBaseUrl = solicitud.MobiControlBaseUrl?.Trim(),
            MobiControlClientId = solicitud.MobiControlClientId?.Trim(),
            MobiControlClientSecret = solicitud.MobiControlClientSecret?.Trim(),
            MobiControlUsuario = solicitud.MobiControlUsuario?.Trim(),
            MobiControlPassword = solicitud.MobiControlPassword,
            MobiControlTimeoutSegundos = solicitud.MobiControlTimeoutSegundos ?? 20,
            FechaCreacion = DateTime.UtcNow,
        };

        if (!string.IsNullOrWhiteSpace(solicitud.MobiControlAtributoFirma))
            empresa.MobiControlAtributoFirma = solicitud.MobiControlAtributoFirma.Trim();
        if (!string.IsNullOrWhiteSpace(solicitud.MobiControlAtributoFecha))
            empresa.MobiControlAtributoFecha = solicitud.MobiControlAtributoFecha.Trim();

        db.Empresas.Add(empresa);
        await db.SaveChangesAsync(ct);

        // Estados base, para que la empresa sirva desde su primera acta.
        await ApplicationDbContextSeed.SembrarEmpresaAsync(db, empresa.EmpresaId, ct);

        return Ok(new EmpresaCreadaResponse(AVista(empresa, 0), llave));
    }

    [HttpPut("{empresaId:int}")]
    public async Task<ActionResult<EmpresaDto>> Actualizar(
        int empresaId, [FromBody] EmpresaAltaRequest solicitud, CancellationToken ct)
    {
        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.EmpresaId == empresaId, ct);
        if (empresa is null) return NotFound(new { message = "No existe esa empresa." });

        if (!string.IsNullOrWhiteSpace(solicitud.Nombre)) empresa.Nombre = solicitud.Nombre.Trim();
        empresa.Nit = solicitud.Nit?.Trim();
        if (!string.IsNullOrWhiteSpace(solicitud.CiudadFirma)) empresa.CiudadFirma = solicitud.CiudadFirma.Trim();

        empresa.MobiControlBaseUrl = solicitud.MobiControlBaseUrl?.Trim();
        empresa.MobiControlClientId = solicitud.MobiControlClientId?.Trim();
        empresa.MobiControlUsuario = solicitud.MobiControlUsuario?.Trim();

        // El secreto y la contraseña solo se tocan si vienen con valor. La consola nunca los
        // muestra, así que al editar cualquier otro campo llegarían vacíos y los borrarían.
        if (!string.IsNullOrWhiteSpace(solicitud.MobiControlClientSecret))
            empresa.MobiControlClientSecret = solicitud.MobiControlClientSecret.Trim();
        if (!string.IsNullOrWhiteSpace(solicitud.MobiControlPassword))
            empresa.MobiControlPassword = solicitud.MobiControlPassword;

        if (!string.IsNullOrWhiteSpace(solicitud.MobiControlAtributoFirma))
            empresa.MobiControlAtributoFirma = solicitud.MobiControlAtributoFirma.Trim();
        if (!string.IsNullOrWhiteSpace(solicitud.MobiControlAtributoFecha))
            empresa.MobiControlAtributoFecha = solicitud.MobiControlAtributoFecha.Trim();
        if (solicitud.MobiControlTimeoutSegundos is { } segundos)
            empresa.MobiControlTimeoutSegundos = segundos;

        empresa.FechaActualizacion = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(AVista(empresa, await ContarActasAsync(empresaId, ct)));
    }

    /// <summary>
    /// Activa o desactiva la empresa. No se borra: sus actas son evidencia de entregas. Una
    /// empresa desactivada deja de aceptar actas nuevas y conserva su histórico.
    /// </summary>
    [HttpPost("{empresaId:int}/activa")]
    public async Task<IActionResult> CambiarEstado(int empresaId, [FromBody] bool activa, CancellationToken ct)
    {
        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.EmpresaId == empresaId, ct);
        if (empresa is null) return NotFound(new { message = "No existe esa empresa." });

        empresa.Activo = activa;
        empresa.FechaActualizacion = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { message = activa ? "Empresa activada." : "Empresa desactivada." });
    }

    /// <summary>
    /// Genera una llave nueva para los equipos. La anterior deja de servir en el acto, así que
    /// hay que reinstalar el formulario en la flota después de rotarla.
    /// </summary>
    [HttpPost("{empresaId:int}/rotar-llave")]
    public async Task<ActionResult<EmpresaCreadaResponse>> RotarLlave(int empresaId, CancellationToken ct)
    {
        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.EmpresaId == empresaId, ct);
        if (empresa is null) return NotFound(new { message = "No existe esa empresa." });

        var llave = LlavesDispositivo.Generar();
        empresa.ApiKeyHash = LlavesDispositivo.Resumir(llave);
        empresa.ApiKeyPrefijo = LlavesDispositivo.Prefijo(llave);
        empresa.ApiKeyRotadaEn = DateTime.UtcNow;
        empresa.FechaActualizacion = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new EmpresaCreadaResponse(AVista(empresa, await ContarActasAsync(empresaId, ct)), llave));
    }
}
