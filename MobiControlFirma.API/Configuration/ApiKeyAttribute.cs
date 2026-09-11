using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using MobiControlFirma.Infrastructure.Identidad;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.API.Configuration;

/// <summary>Quién puede llamar a un endpoint.</summary>
public enum RolApi
{
    /// <summary>El formulario instalado en el equipo. Solo registra y consulta su propio estado.</summary>
    Dispositivo,

    /// <summary>Consola administrativa: histórico completo y reintentos.</summary>
    Administrador,
}

/// <summary>
/// Exige la cabecera <c>X-Api-Key</c>. Es la autenticación posible aquí: el formulario corre
/// dentro del navegador del dispositivo, sin usuario que inicie sesión.
///
/// La llave del dispositivo vive en el HTML instalado, así que conviene inyectarla desde un
/// atributo personalizado de MobiControl (<c>%CustomAttr:ApiKey%</c>) en vez de escribirla en
/// el archivo: así se rota desde la consola sin volver a desplegar el formulario.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAttribute(RolApi rolMinimo = RolApi.Dispositivo) : Attribute, IAuthorizationFilter
{
    public const string NombreCabecera = "X-Api-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Un usuario con sesión iniciada en la consola ya se identificó con credenciales
        // propias, que es una garantía mayor que una llave compartida: no se le pide además
        // la cabecera. Las llaves siguen existiendo para el formulario del dispositivo, que
        // corre sin nadie que pueda iniciar sesión.
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            return;

        var opciones = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<SeguridadOptions>>().Value;

        var enviada = context.HttpContext.Request.Headers[NombreCabecera].FirstOrDefault();

        // También se acepta por query string, y solo por eso: al PDF y a la firma se llega
        // navegando (una pestaña nueva, un enlace en un correo) y ahí no hay forma de poner
        // cabeceras. Para el resto de endpoints siempre se usa la cabecera.
        if (string.IsNullOrWhiteSpace(enviada))
            enviada = context.HttpContext.Request.Query["apiKey"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(enviada))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Falta la cabecera X-Api-Key." });
            return;
        }

        // La llave de administrador es la del sistema entero, no la de una empresa: equivale a
        // un superadministrador y por eso no fija ninguna empresa en la petición.
        if (SonIguales(opciones.ApiKeyAdministrador, enviada!))
        {
            context.HttpContext.Items[ContextoEmpresa.ClaveSuperAdmin] = true;
            return;
        }

        if (rolMinimo == RolApi.Administrador)
        {
            context.Result = new UnauthorizedObjectResult(
                new { message = "Esta operación requiere una sesión de administrador." });
            return;
        }

        // Llave de equipo: identifica a la empresa además de autenticar. Se busca por el
        // resumen, nunca por la llave, que no está guardada en ninguna parte.
        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var resumen = LlavesDispositivo.Resumir(enviada!);

        var empresaId = db.Empresas
            .Where(e => e.Activo && e.ApiKeyHash == resumen)
            .Select(e => (int?)e.EmpresaId)
            .FirstOrDefault();

        if (empresaId is null)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "La llave de acceso no es válida." });
            return;
        }

        // A partir de aquí toda consulta de esta petición queda acotada a esa empresa.
        context.HttpContext.Items[ContextoEmpresa.ClaveEnContexto] = empresaId.Value;
    }

    /// <summary>
    /// Si quien llama puede actuar como administrador: o entró con usuario y contraseña, o
    /// presentó la llave de administrador. Lo usa el alta de usuarios, que vive fuera de los
    /// controladores y por tanto fuera de este filtro.
    /// </summary>
    public static bool EsAdministrador(HttpContext contexto)
    {
        if (contexto.User.Identity?.IsAuthenticated == true) return true;

        var opciones = contexto.RequestServices.GetRequiredService<IOptions<SeguridadOptions>>().Value;

        var enviada = contexto.Request.Headers[NombreCabecera].FirstOrDefault()
            ?? contexto.Request.Query["apiKey"].FirstOrDefault();

        return !string.IsNullOrWhiteSpace(enviada) && SonIguales(opciones.ApiKeyAdministrador, enviada);
    }

    /// <summary>
    /// Comparación de tiempo constante. Con un <c>==</c> normal, el tiempo de respuesta delata
    /// cuántos caracteres del principio acertó quien esté probando llaves.
    /// </summary>
    private static bool SonIguales(string? esperada, string enviada)
    {
        if (string.IsNullOrWhiteSpace(esperada)) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(esperada),
            Encoding.UTF8.GetBytes(enviada));
    }
}
