using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.API.Configuration;

/// <summary>One no contestó a tiempo: no se sabe si la credencial es buena, así que no se puede rechazar como mala.</summary>
public class OneNoDisponibleException() : Exception("One no respondió a tiempo al validar la credencial.");

/// <summary>
/// Valida una credencial de integración emitida por One (api key + secreto) y la traduce a la
/// empresa local. Es la forma en que un sistema externo llama a este API: la credencial se emite,
/// se revoca y se audita en One, por empresa, igual que las de cualquier otra app del catálogo.
///
/// One responde con su /integration/verify de qué empresa y de qué app es la credencial. Aquí solo
/// se acepta si es de esta app: una credencial de otra app de la misma empresa no abre nada.
/// </summary>
public class CredencialesOne(
    IHttpClientFactory clientes,
    IMemoryCache cache,
    EmpresasOne empresasOne,
    ApplicationDbContext db,
    IOptions<OneOptions> opciones,
    ILogger<CredencialesOne> logger)
{
    /// <summary>
    /// Cuánto se recuerda una validación. Evita una llamada a One por petición; a cambio, revocar
    /// una credencial en One tarda a lo sumo esto en notarse aquí.
    /// </summary>
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(2);

    /// <summary>Empresa local de la credencial, o null si One no la reconoce para esta app.</summary>
    public async Task<int?> ResolverEmpresaAsync(string apiKey, string apiSecret, CancellationToken ct)
    {
        // Se indexa por el resumen del par, no por el par: un volcado de memoria no debe dejar
        // credenciales completas a la vista.
        var clave = "one:cred:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{apiKey}\n{apiSecret}")));

        if (!cache.TryGetValue<Verificacion>(clave, out var verificacion) || verificacion is null)
        {
            verificacion = await VerificarAsync(apiKey, apiSecret, ct);
            if (verificacion is null) return null;

            cache.Set(clave, verificacion, Vigencia);
        }

        var existente = await db.Empresas.AsNoTracking()
            .Where(e => e.OneTenantId == verificacion.TenantId)
            .Select(e => new { e.EmpresaId, e.Activo })
            .FirstOrDefaultAsync(ct);

        if (existente is not null) return existente.Activo ? existente.EmpresaId : null;

        // La empresa todavía no tiene fila local: nadie de ella ha entrado a la consola. Se crea
        // igual que al primer ingreso; el nombre se corrige con el slug hasta que entre alguien.
        await empresasOne.SincronizarAsync(
            [new EmpresaEnOne(verificacion.TenantId, verificacion.TenantSlug, verificacion.TenantSlug, null)], ct);

        return await db.Empresas.AsNoTracking()
            .Where(e => e.OneTenantId == verificacion.TenantId && e.Activo)
            .Select(e => (int?)e.EmpresaId)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Verificacion?> VerificarAsync(string apiKey, string apiSecret, CancellationToken ct)
    {
        try
        {
            var http = clientes.CreateClient(ManejadorAutenticacionOne.ClienteHttp);

            using var peticion = new HttpRequestMessage(HttpMethod.Get, "api/v1/integration/verify");
            peticion.Headers.Add("X-Api-Key", apiKey);
            peticion.Headers.Add("X-Api-Secret", apiSecret);

            using var respuesta = await http.SendAsync(peticion, ct);

            if (!respuesta.IsSuccessStatusCode)
            {
                logger.LogInformation("One rechazó una credencial de integración ({Codigo}).", (int)respuesta.StatusCode);
                return null;
            }

            var introspeccion = await respuesta.Content.ReadFromJsonAsync<Introspeccion>(ct);

            if (introspeccion is not { Active: true })
                return null;

            if (!string.Equals(introspeccion.AppSlug, opciones.Value.AppSlug, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Se presentó una credencial de One de la app {App}, no de {Esperada}.",
                    introspeccion.AppSlug, opciones.Value.AppSlug);
                return null;
            }

            return new Verificacion(introspeccion.TenantId, introspeccion.TenantSlug);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Es el límite del HttpClient, no el cliente que se fue: One está lento o reiniciando.
            logger.LogWarning("One no respondió a tiempo al verificar una credencial.");
            throw new OneNoDisponibleException();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "No se pudo conectar con One para verificar la credencial.");
            throw new OneNoDisponibleException();
        }
        catch (System.Text.Json.JsonException ex)
        {
            // One contestó algo que no es la introspección esperada: no hay cómo dar por buena
            // la credencial, y tampoco es una caída; se rechaza.
            logger.LogError(ex, "One devolvió una verificación de credencial ilegible.");
            return null;
        }
    }

    private sealed record Verificacion(Guid TenantId, string TenantSlug);

    private sealed record Introspeccion(
        [property: JsonPropertyName("active")] bool Active,
        [property: JsonPropertyName("tenantId")] Guid TenantId,
        [property: JsonPropertyName("tenantSlug")] string TenantSlug,
        [property: JsonPropertyName("appSlug")] string AppSlug);
}
