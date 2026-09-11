using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Infrastructure.Identidad;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.API.Controllers;

public record UsuarioDto(
    string Id, string Correo, string? NombreCompleto, int? EmpresaId, string? Empresa,
    string Rol, bool Activo, DateTime FechaCreacion);

public record UsuarioAltaRequest(string Correo, string Clave, string? NombreCompleto, int? EmpresaId, string? Rol);

public record CambioClaveRequest(string Clave);

/// <summary>Quién soy, para que la consola sepa qué puede mostrar.</summary>
public record IdentidadDto(string Correo, string? NombreCompleto, string Rol, int? EmpresaId, string? Empresa);

/// <summary>
/// Usuarios de la consola. El superadministrador los crea en cualquier empresa; un
/// administrador de empresa solo dentro de la suya, y no puede ascender a nadie por encima de
/// su propio alcance.
/// </summary>
[ApiController]
[Route("api/v1/usuarios")]
[Authorize(Roles = $"{Roles.SuperAdministrador},{Roles.AdministradorEmpresa}")]
public class UsuariosController(
    UserManager<UsuarioAdmin> usuarios,
    ApplicationDbContext db,
    IContextoEmpresa contexto) : ControllerBase
{
    private bool EsSuper => contexto.EsSuperAdministrador;

    private async Task<UsuarioDto> AVistaAsync(UsuarioAdmin usuario, IReadOnlyDictionary<int, string> empresas)
    {
        var roles = await usuarios.GetRolesAsync(usuario);

        return new UsuarioDto(
            usuario.Id,
            usuario.Email ?? usuario.UserName ?? "",
            usuario.NombreCompleto,
            usuario.EmpresaId,
            usuario.EmpresaId is { } id ? empresas.GetValueOrDefault(id) : null,
            roles.FirstOrDefault() ?? Roles.AdministradorEmpresa,
            usuario.Activo,
            usuario.FechaCreacion);
    }

    private Task<Dictionary<int, string>> NombresEmpresasAsync(CancellationToken ct) =>
        db.Empresas.AsNoTracking().ToDictionaryAsync(e => e.EmpresaId, e => e.Nombre, ct);

    /// <summary>
    /// La consola pregunta esto al entrar: de ahí decide si muestra el módulo de empresas y si
    /// el listado de actas es de una sola o de todas.
    /// </summary>
    [HttpGet("yo")]
    public async Task<ActionResult<IdentidadDto>> Yo(CancellationToken ct)
    {
        var usuario = await usuarios.GetUserAsync(User);
        if (usuario is null) return Unauthorized(new { message = "La sesión ya no es válida." });

        var roles = await usuarios.GetRolesAsync(usuario);
        var empresa = usuario.EmpresaId is { } id
            ? await db.Empresas.AsNoTracking().Where(e => e.EmpresaId == id).Select(e => e.Nombre).FirstOrDefaultAsync(ct)
            : null;

        return Ok(new IdentidadDto(
            usuario.Email ?? usuario.UserName ?? "",
            usuario.NombreCompleto,
            roles.FirstOrDefault() ?? Roles.AdministradorEmpresa,
            usuario.EmpresaId,
            empresa));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsuarioDto>>> Listar([FromQuery] int? empresaId, CancellationToken ct)
    {
        var consulta = usuarios.Users.AsNoTracking();

        // Un administrador de empresa solo ve los suyos, pida lo que pida por query string.
        consulta = EsSuper
            ? (empresaId is { } filtro ? consulta.Where(u => u.EmpresaId == filtro) : consulta)
            : consulta.Where(u => u.EmpresaId == contexto.EmpresaId);

        var lista = await consulta.OrderBy(u => u.Email).ToListAsync(ct);
        var empresas = await NombresEmpresasAsync(ct);

        var salida = new List<UsuarioDto>(lista.Count);
        foreach (var usuario in lista) salida.Add(await AVistaAsync(usuario, empresas));

        return Ok(salida);
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Crear([FromBody] UsuarioAltaRequest solicitud, CancellationToken ct)
    {
        var correo = solicitud.Correo?.Trim();
        if (string.IsNullOrWhiteSpace(correo))
            return BadRequest(new { message = "Hace falta el correo." });

        if (string.IsNullOrWhiteSpace(solicitud.Clave))
            return BadRequest(new { message = "Hace falta la contraseña." });

        var rol = string.IsNullOrWhiteSpace(solicitud.Rol) ? Roles.AdministradorEmpresa : solicitud.Rol.Trim();
        if (!Roles.Todos.Contains(rol))
            return BadRequest(new { message = $"El rol {rol} no existe." });

        // Un administrador de empresa no puede crear superadministradores ni sembrar usuarios en
        // otra empresa: sería darse a sí mismo un alcance que no tiene.
        int? empresaId;
        if (EsSuper)
        {
            empresaId = rol == Roles.SuperAdministrador ? null : solicitud.EmpresaId;

            if (rol != Roles.SuperAdministrador && empresaId is null)
                return BadRequest(new { message = "Un administrador de empresa tiene que pertenecer a una." });
        }
        else
        {
            if (rol == Roles.SuperAdministrador)
                return Forbid();

            empresaId = contexto.EmpresaId;
        }

        if (empresaId is { } id && !await db.Empresas.AnyAsync(e => e.EmpresaId == id, ct))
            return BadRequest(new { message = "No existe esa empresa." });

        if (await usuarios.FindByEmailAsync(correo) is not null)
            return BadRequest(new { message = "Ya hay un usuario con ese correo." });

        var usuario = new UsuarioAdmin
        {
            UserName = correo,
            Email = correo,
            EmailConfirmed = true,
            NombreCompleto = solicitud.NombreCompleto?.Trim(),
            EmpresaId = empresaId,
        };

        var creado = await usuarios.CreateAsync(usuario, solicitud.Clave);
        if (!creado.Succeeded)
            return BadRequest(new { message = string.Join(" ", creado.Errors.Select(e => e.Description)) });

        await usuarios.AddToRoleAsync(usuario, rol);

        return Ok(await AVistaAsync(usuario, await NombresEmpresasAsync(ct)));
    }

    /// <summary>
    /// Activa o desactiva un usuario. No se borra: sus actas y su rastro en la bitácora siguen
    /// haciendo referencia a él.
    /// </summary>
    [HttpPost("{id}/activo")]
    public async Task<IActionResult> CambiarEstado(string id, [FromBody] bool activo, CancellationToken ct)
    {
        var usuario = await usuarios.FindByIdAsync(id);
        if (usuario is null) return NotFound(new { message = "No existe ese usuario." });
        if (!PuedeTocar(usuario)) return Forbid();

        // Sin esta guarda, el último superadministrador podría desactivarse a sí mismo y dejar
        // el sistema sin nadie capaz de crear empresas ni usuarios.
        if (!activo && usuario.Id == usuarios.GetUserId(User))
            return BadRequest(new { message = "No puedes desactivar tu propio usuario." });

        usuario.Activo = activo;
        await usuarios.UpdateAsync(usuario);

        return Ok(new { message = activo ? "Usuario activado." : "Usuario desactivado." });
    }

    /// <summary>Restablece la contraseña. Se usa cuando alguien la pierde, no para rotarla sola.</summary>
    [HttpPost("{id}/clave")]
    public async Task<IActionResult> CambiarClave(string id, [FromBody] CambioClaveRequest solicitud)
    {
        var usuario = await usuarios.FindByIdAsync(id);
        if (usuario is null) return NotFound(new { message = "No existe ese usuario." });
        if (!PuedeTocar(usuario)) return Forbid();

        var token = await usuarios.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await usuarios.ResetPasswordAsync(usuario, token, solicitud.Clave);

        if (!resultado.Succeeded)
            return BadRequest(new { message = string.Join(" ", resultado.Errors.Select(e => e.Description)) });

        return Ok(new { message = "Contraseña actualizada." });
    }

    /// <summary>Un administrador de empresa solo alcanza a los usuarios de la suya.</summary>
    private bool PuedeTocar(UsuarioAdmin usuario) =>
        EsSuper || (usuario.EmpresaId is { } id && id == contexto.EmpresaId);
}
