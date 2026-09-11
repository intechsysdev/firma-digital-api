using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MobiControlFirma.Infrastructure.Identidad;

/// <summary>
/// Mete la empresa del usuario dentro del token, junto a sus roles.
///
/// Va en el claim y no se consulta a la base en cada petición por dos razones: el aislamiento
/// lo aplica el contexto de datos, que a su vez necesitaría consultar la base para saber por
/// qué filtrar —una dependencia circular—; y porque hacerlo así lo convierte en una lectura
/// más por cada llamada al API.
/// </summary>
public class FabricaClaimsUsuario(
    UserManager<UsuarioAdmin> usuarios,
    IOptions<IdentityOptions> opciones)
    : UserClaimsPrincipalFactory<UsuarioAdmin>(usuarios, opciones)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(UsuarioAdmin usuario)
    {
        var identidad = await base.GenerateClaimsAsync(usuario);

        if (usuario.EmpresaId is { } empresaId)
            identidad.AddClaim(new Claim(ClaimsPropios.Empresa, empresaId.ToString()));

        foreach (var rol in await UserManager.GetRolesAsync(usuario))
            identidad.AddClaim(new Claim(ClaimTypes.Role, rol));

        return identidad;
    }
}
