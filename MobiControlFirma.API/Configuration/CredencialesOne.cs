using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.API.Configuration;

/// <summary>Resultado de validar una credencial de One.</summary>
/// <param name="EmpresaId">Empresa local de la credencial cuando es válida.</param>
/// <param name="OneNoDisponible">
/// One no contestó: no se sabe si la credencial es buena, así que no se puede rechazar como mala.
/// </param>
public sealed record ResultadoCredencial(int? EmpresaId, bool OneNoDisponible)
{
    public static readonly ResultadoCredencial Invalida = new(null, false);
    public static readonly ResultadoCredencial SinOne = new(null, true);
}

/// <summary>
/// Valida una credencial de integración emitida por One (api key + secreto) y la traduce a la
/// empresa local. Es la forma en que un sistema externo llama a este API: la credencial se emite,
/// se revoca y se audita en One, por empresa, igual que las de cualquier otra app del catálogo.
///
/// One responde con su /integration/verify de qué empresa y de qué app es la credencial. Aquí solo
/// se acepta si es de esta app: una credencial de otra app de la misma empresa no abre nada.
///
/// Devuelve un resultado en vez de lanzar excepciones: lo consume el filtro de llaves, por el que
/// pasa cada petición de los equipos, y ahí conviene un flujo sin try/catch.
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

    public async Task<ResultadoCredencial> ResolverEmpresaAsync(string apiKey, string apiSecret, CancellationToken ct)
    {
        // Se indexa por el resumen del par, no por el par: un volcado de memoria no debe dejar
        // credenciales completas a la vista.
        var clave = "one:cred:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{apiKey}\n{apiSecret}")));

        if (!cache.TryGetValue<Verificacion>(clave, out var verificacion) || verificacion is null)
        {
            var (resultado, oneRespondio) = await VerificarAsync(apiKey, apiSecret, ct);
            if (!oneRespondio) return ResultadoCredencial.SinOne;
            if (resultado is null) return ResultadoCredencial.Invalida;

            verificacion = resultado;
            cache.Set(clave, verificacion, Vigencia);
        }

        var existente = await db.Empresas.AsNoTracking()
            .Where(e => e.OneTenantId == verificacion.TenantId)
            .Select(e => new { e.EmpresaId, e.Activo })
            .FirstOrDefaultAsync(ct);

        if (existente is not null)
            return existente.Activo ? new ResultadoCredencial(existente.EmpresaId, false) : ResultadoCredencial.Invalida;

        // La empresa todavía no tiene fila local: nadie de ella ha entrado a la consola. Se crea
        // igual que al primer ingreso; el nombre se corrige con el slug hasta que entre alguien.
        await empresasOne.SincronizarAsync(
            [new EmpresaEnOne(verificacion.TenantId, verificacion.TenantSlug, verificacion.TenantSlug, null)], ct);

        var creada = await db.Empresas.AsNoTracking()
            .Where(e => e.OneTenantId == verificacion.TenantId && e.Activo)
            .Select(e => (int?)e.EmpresaId)
            .FirstOrDefaultAsync(ct);

        return creada is null ? ResultadoCredencial.Invalida : new ResultadoCredencial(creada, false);
    }

    /// <summary>
    /// La verificación, o null si One la rechazó; OneRespondio en false si One no contestó (caído,
    /// reiniciando o lento), que no es lo mismo que una credencial mala.
    /// </summary>
    private async Task<(Verificacion? Resultado, bool OneRespondio)> VerificarAsync(
        string apiKey, string apiSecret, CancellationToken ct)
    {
        try
        {
            var http = clientes.CreateClient(ManejadorAutenticacionOne.ClienteHttp);

            using var peticion = new HttpRequestMessage(HttpMethod.Get, "api/v1/integration/verify");
            peticion.Headers.Add("X-Api-Key", apiKey);
            peticion.Headers.Add("X-Api-Secret", apiSecret);

            using var respuesta = await http.SendAsync(peticion, ct);

            // 5xx es One con problemas, no la credencial: se trata igual que no contestar.
            if ((int)respuesta.StatusCode >= 500)
            {
                logger.LogWarning("One respondió {Codigo} al verificar una credencial.", (int)respuesta.StatusCode);
                return (null, false);
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                logger.LogInformation("One rechazó una credencial de integración ({Codigo}).", (int)respuesta.StatusCode);
                return (null, true);
            }

            var introspeccion = await respuesta.Content.ReadFromJsonAsync<Introspeccion>(ct);

            if (introspeccion is not { Active: true })
                return (null, true);

            if (!string.Equals(introspeccion.AppSlug, opciones.Value.AppSlug, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Se presentó una credencial de One de la app {App}, no de {Esperada}.",
                    introspeccion.AppSlug, opciones.Value.AppSlug);
                return (null, true);
            }

            return (new Verificacion(introspeccion.TenantId, introspeccion.TenantSlug), true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Un JSON ilegible es One contestando mal: se rechaza. Cualquier otra cosa —el límite
            // del HttpClient, un error de red— es One sin contestar.
            if (ex is System.Text.Json.JsonException)
            {
                logger.LogError(ex, "One devolvió una verificación de credencial ilegible.");
                return (null, true);
            }

            logger.LogWarning(ex, "One no respondió al verificar una credencial.");
            return (null, false);
        }
    }

    private sealed record Verificacion(Guid TenantId, string TenantSlug);

    private sealed record Introspeccion(
        [property: JsonPropertyName("active")] bool Active,
        [property: JsonPropertyName("tenantId")] Guid TenantId,
        [property: JsonPropertyName("tenantSlug")] string TenantSlug,
        [property: JsonPropertyName("appSlug")] string AppSlug);
}
