using System.Security.Claims;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Infrastructure.Identidad;

namespace MobiControlFirma.API.Configuration;

/// <summary>
/// Resuelve a qué empresa pertenece la petición en curso. Hay dos maneras de llegar al API y
/// cada una la identifica distinto:
///
/// - Un usuario de la consola la trae en su token, puesta al iniciar sesión.
/// - Un equipo la demuestra con la llave que lleva instalada; el filtro de llaves resuelve la
///   empresa y la deja aquí antes de que la petición llegue al controlador.
/// </summary>
public class ContextoEmpresa(IHttpContextAccessor acceso) : IContextoEmpresa
{
    /// <summary>Clave con la que el filtro de llaves deja la empresa resuelta.</summary>
    public const string ClaveEnContexto = "EmpresaId";

    /// <summary>Clave con la que el filtro de llaves marca que se usó la llave de administrador.</summary>
    public const string ClaveSuperAdmin = "EsSuperAdministrador";

    private HttpContext? Contexto => acceso.HttpContext;

    public int? EmpresaId
    {
        get
        {
            if (Contexto is null) return null;

            // Lo que dejó el filtro de llaves manda: es de esta misma petición.
            if (Contexto.Items.TryGetValue(ClaveEnContexto, out var valor) && valor is int id)
                return id;

            var claim = Contexto.User.FindFirst(ClaimsPropios.Empresa)?.Value;
            return int.TryParse(claim, out var delToken) ? delToken : null;
        }
    }

    public bool EsSuperAdministrador
    {
        get
        {
            if (Contexto is null) return false;

            if (Contexto.Items.TryGetValue(ClaveSuperAdmin, out var valor) && valor is true)
                return true;

            return Contexto.User.IsInRole(Roles.SuperAdministrador);
        }
    }

    public int EmpresaRequerida => EmpresaId
        ?? throw new InvalidOperationException(
            "La petición no tiene empresa asociada. Un registro sin empresa no tiene dueño.");
}
