using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.API.Configuration;

/// <summary>
/// Autentica con el enlace de firma. Quien firma no tiene usuario ni llave: el token del enlace
/// es su credencial, y dice además a qué empresa pertenece la petición.
///
/// Hace el mismo trabajo que el filtro de llaves para los equipos: deja la empresa resuelta
/// antes de que llegue al controlador, y a partir de ahí toda consulta queda acotada a ella.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class EnlaceFirmaAttribute : Attribute, IAsyncAuthorizationFilter
{
    /// <summary>Parámetro de ruta que trae el token.</summary>
    public const string ParametroRuta = "token";

    /// <summary>Clave con la que queda la solicitud resuelta en la petición.</summary>
    public const string ClaveSolicitud = "SolicitudUid";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var servicios = context.HttpContext.RequestServices;
        var token = context.RouteData.Values[ParametroRuta] as string;

        if (string.IsNullOrWhiteSpace(token) ||
            servicios.GetRequiredService<IEnlacesFirma>().LeerToken(token) is not { } solicitudUid)
        {
            context.Result = NoValido();
            return;
        }

        // La tabla de solicitudes está filtrada por empresa y todavía no hay empresa: se busca
        // sin filtro, y lo único que se saca de aquí es justamente cuál es.
        var db = servicios.GetRequiredService<ApplicationDbContext>();
        var empresaId = await db.Solicitudes
            .IgnoreQueryFilters()
            .Where(s => s.SolicitudUid == solicitudUid && s.Empresa.Activo)
            .Select(s => (int?)s.EmpresaId)
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (empresaId is null)
        {
            context.Result = NoValido();
            return;
        }

        context.HttpContext.Items[ContextoEmpresa.ClaveEnContexto] = empresaId.Value;
        // Aunque quien abra el enlace tenga una sesión de plataforma en el mismo navegador, aquí
        // actúa como firmante de esta empresa y nada más.
        context.HttpContext.Items[ContextoEmpresa.ClaveSuperAdmin] = false;
        context.HttpContext.Items[ClaveSolicitud] = solicitudUid;
    }

    /// <summary>
    /// Mismo mensaje para un token inventado y para uno de una empresa desactivada: distinguirlos
    /// le diría a quien prueba enlaces cuáles existieron.
    /// </summary>
    private static NotFoundObjectResult NoValido() =>
        new(new { message = "El enlace de firma no es válido. Revisa que lo hayas copiado completo." });
}
