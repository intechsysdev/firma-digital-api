using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.Infrastructure.One;

/// <summary>
/// Trae la configuración de cada empresa desde One, con la credencial que esa empresa tiene
/// emitida para la app "firma-digital".
///
/// Se cachea por unos minutos: registrar un acta dispara varias lecturas de configuración —el
/// cliente de MobiControl, el encolado del correo, el envío en segundo plano— y sin caché cada
/// firma se convertiría en varias llamadas de red a One antes de hacer nada útil.
/// </summary>
public class ProveedorConfiguracionOne(
    HttpClient http,
    ApplicationDbContext db,
    IMemoryCache cache,
    ILogger<ProveedorConfiguracionOne> logger) : IProveedorConfiguracion
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(5);

    private static string Clave(int empresaId) => $"one:config:{empresaId}";

    public void Olvidar(int empresaId) => cache.Remove(Clave(empresaId));

    public async Task<ConfiguracionEmpresa?> ObtenerAsync(int empresaId, CancellationToken ct = default)
    {
        if (cache.TryGetValue<ConfiguracionEmpresa>(Clave(empresaId), out var enCache) && enCache is not null)
            return enCache;

        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmpresaId == empresaId, ct);

        if (empresa is null || !empresa.OneConfigurado)
        {
            logger.LogWarning("La empresa {Empresa} no tiene credencial de One configurada.", empresaId);
            return null;
        }

        try
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Get, "api/v1/integration/config");
            peticion.Headers.Add("X-Api-Key", empresa.OneApiKey);
            peticion.Headers.Add("X-Api-Secret", empresa.OneApiSecret);

            using var respuesta = await http.SendAsync(peticion, ct);

            if (!respuesta.IsSuccessStatusCode)
            {
                var detalle = await respuesta.Content.ReadAsStringAsync(ct);
                logger.LogError("One rechazó la configuración de {Empresa} ({Codigo}): {Detalle}",
                    empresa.Nombre, (int)respuesta.StatusCode, detalle[..Math.Min(400, detalle.Length)]);
                return null;
            }

            var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaOne>(ct);
            if (cuerpo is null) return null;

            var configuracion = Traducir(cuerpo);

            cache.Set(Clave(empresaId), configuracion, Vigencia);
            return configuracion;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error consultando la configuración de {Empresa} en One.", empresa.Nombre);
            return null;
        }
    }

    /// <summary>
    /// Pasa del diccionario plano de One a algo tipado. Los valores por defecto se repiten aquí
    /// porque una variable que la empresa no configuró llega ausente, y el API tiene que seguir
    /// funcionando con algo razonable en vez de reventar.
    /// </summary>
    private static ConfiguracionEmpresa Traducir(RespuestaOne cuerpo)
    {
        string? V(string clave) =>
            cuerpo.Settings.TryGetValue(clave, out var valor) && !string.IsNullOrWhiteSpace(valor)
                ? valor.Trim()
                : null;

        return new ConfiguracionEmpresa(
            TenantSlug: cuerpo.Tenant.Slug,
            TenantNombre: cuerpo.Tenant.Name,
            MobiControlBaseUrl: V("MOBICONTROL_BASE_URL"),
            MobiControlClientId: V("MOBICONTROL_CLIENT_ID"),
            MobiControlClientSecret: V("MOBICONTROL_CLIENT_SECRET"),
            MobiControlUsuario: V("MOBICONTROL_USUARIO"),
            MobiControlPassword: V("MOBICONTROL_PASSWORD"),
            MobiControlAtributoFirma: V("MOBICONTROL_ATRIBUTO_FIRMA") ?? "Firma de entrega",
            MobiControlAtributoFecha: V("MOBICONTROL_ATRIBUTO_FECHA") ?? "Fecha de entrega",
            MobiControlTimeoutSegundos: int.TryParse(V("MOBICONTROL_TIMEOUT_SEGUNDOS"), out var t) ? t : 20,
            CorreosCopia: V("CORREOS_COPIA"),
            InfobipBaseUrl: V("INFOBIP_BASE_URL"),
            InfobipApiKey: V("INFOBIP_API_KEY"),
            InfobipRemitente: V("INFOBIP_REMITENTE"),
            InfobipNombreRemitente: V("INFOBIP_NOMBRE_REMITENTE"),
            CiudadFirma: V("CIUDAD_FIRMA") ?? "Cali",
            CallbackUrl: V("CALLBACK_URL"),
            CallbackSecreto: V("CALLBACK_SECRET"),
            ConfigVersion: cuerpo.ConfigVersion ?? string.Empty);
    }

    private sealed record RespuestaOne(
        [property: JsonPropertyName("tenant")] TenantOne Tenant,
        [property: JsonPropertyName("settings")] Dictionary<string, string?> Settings,
        [property: JsonPropertyName("configVersion")] string? ConfigVersion);

    private sealed record TenantOne(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("slug")] string Slug);
}
