using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.API.Configuration;

/// <summary>
/// Quién puede cambiar la consola de MobiControl de la empresa: su dueño o administrador en One, o
/// la plataforma. Los miembros leen y crean actas, pero no tocan los atributos de los equipos.
/// </summary>
public class PermisosEmpresa(IContextoEmpresa empresa, ApplicationDbContext db)
{
    public async Task<bool> PuedeAdministrarAsync(ClaimsPrincipal usuario, CancellationToken ct)
    {
        if (usuario.IsInRole(One.RolPlataforma)) return true;

        if (empresa.EmpresaId is not { } empresaId) return false;

        var tenantId = await db.Empresas.AsNoTracking()
            .Where(e => e.EmpresaId == empresaId)
            .Select(e => e.OneTenantId)
            .FirstAsync(ct);

        return ResolucionTenantMiddleware.RolEn(usuario, tenantId) is "Owner" or "Admin";
    }
}
