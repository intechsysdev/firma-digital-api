using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MobiControlFirma.Infrastructure.Identidad;

/// <summary>
/// Añade al ingreso una condición que Identity no conoce: el usuario tiene que estar activo, y
/// su empresa también.
///
/// Sin esto, desactivar a alguien en la consola no le impediría entrar —Identity solo mira
/// contraseña, bloqueo y confirmaciones—, y desactivar una empresa dejaría a su gente operando
/// como si nada.
/// </summary>
public class GestorIngreso(
    UserManager<UsuarioAdmin> usuarios,
    IHttpContextAccessor contexto,
    IUserClaimsPrincipalFactory<UsuarioAdmin> fabricaClaims,
    IOptions<IdentityOptions> opciones,
    ILogger<SignInManager<UsuarioAdmin>> registro,
    IAuthenticationSchemeProvider esquemas,
    IUserConfirmation<UsuarioAdmin> confirmacion,
    Persistence.ApplicationDbContext db)
    : SignInManager<UsuarioAdmin>(usuarios, contexto, fabricaClaims, opciones, registro, esquemas, confirmacion)
{
    public override async Task<bool> CanSignInAsync(UsuarioAdmin usuario)
    {
        if (!usuario.Activo)
        {
            registro.LogInformation("Ingreso rechazado: el usuario {Usuario} está desactivado.", usuario.Email);
            return false;
        }

        if (usuario.EmpresaId is { } empresaId)
        {
            var empresaActiva = await db.Empresas
                .Where(e => e.EmpresaId == empresaId)
                .Select(e => e.Activo)
                .FirstOrDefaultAsync();

            if (!empresaActiva)
            {
                registro.LogInformation(
                    "Ingreso rechazado: la empresa de {Usuario} está desactivada.", usuario.Email);
                return false;
            }
        }

        return await base.CanSignInAsync(usuario);
    }
}
