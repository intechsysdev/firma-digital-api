using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Domain.Entities;
using MobiControlFirma.Domain.Enums;

namespace MobiControlFirma.Infrastructure.MobiControl;

/// <summary>
/// Credenciales y parámetros de la consola de MobiControl. Antes vivían escritos dentro del
/// HTML instalado en cada equipo, donde cualquiera con el dispositivo en la mano podía leerlas.
/// </summary>
public class MobiControlOptions
{
    public const string SectionName = "MobiControl";

    /// <summary>Ej.: https://s002007.mobicontrolcloud.com/mobicontrol</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Atributo personalizado que marca el acta como firmada.</summary>
    public string AtributoFirma { get; set; } = "Firma de entrega";

    /// <summary>Atributo personalizado donde se escribe la fecha de entrega.</summary>
    public string AtributoFecha { get; set; } = "Fecha de entrega";

    public int TimeoutSegundos { get; set; } = 20;

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(Usuario) &&
        !string.IsNullOrWhiteSpace(Password);
}

/// <summary>
/// Cliente de la API de MobiControl: pide el token, marca los atributos personalizados del
/// equipo y le fuerza un check-in para que el cambio se refleje de inmediato en la consola.
/// </summary>
public class ClienteMobiControl(
    HttpClient http,
    IProveedorConfiguracion configuracion,
    IContextoEmpresa contextoEmpresa,
    ILogger<ClienteMobiControl> logger) : IClienteMobiControl
{
    // El token dura ~una hora y se reutiliza entre actas: pedir uno por firma multiplicaba por
    // tres las llamadas a la consola sin ninguna ganancia. La caché es por empresa porque cada
    // una tiene su propia consola: un token de una no sirve —ni debe servir— en otra.
    private static readonly ConcurrentDictionary<int, TokenEnCache> Tokens = new();
    private static readonly SemaphoreSlim Candado = new(1, 1);

    private sealed record TokenEnCache(string Token, DateTime Expira);

    private async Task<ConfiguracionEmpresa?> ConfigAsync(CancellationToken ct)
    {
        if (contextoEmpresa.EmpresaId is not { } id) return null;
        return await configuracion.ObtenerAsync(id, ct);
    }

    public async Task<bool> EstaConfiguradoAsync(CancellationToken ct = default) =>
        await ConfigAsync(ct) is { MobiControlConfigurado: true };

    public async Task<IReadOnlyList<ResultadoIntegracion>> MarcarEntregaFirmadaAsync(
        string deviceId, DateOnly fechaEntrega, CancellationToken ct = default)
    {
        var resultados = new List<ResultadoIntegracion>();
        var config = await ConfigAsync(ct);

        if (config is null || !config.MobiControlConfigurado)
        {
            resultados.Add(new ResultadoIntegracion(
                TipoAccionIntegracion.ObtenerToken, false, null,
                config is null
                    ? "No se pudo obtener de One la configuración de la empresa."
                    : $"El tenant {config.TenantNombre} no tiene configurada su consola de MobiControl en One."));
            return resultados;
        }

        var empresaId = contextoEmpresa.EmpresaRequerida;
        var (token, resultadoToken) = await ObtenerTokenAsync(empresaId, config, ct);
        resultados.Add(resultadoToken);
        if (token is null) return resultados;

        resultados.Add(await ActualizarAtributosAsync(empresaId, config, token, deviceId, fechaEntrega, ct));

        // El check-in se pide aunque la actualización de atributos haya fallado: es barato y,
        // si el fallo fue de red y no de datos, deja el equipo reportando igual.
        resultados.Add(await CheckInAsync(empresaId, config, token, deviceId, ct));

        return resultados;
    }

    /// <summary>
    /// La barra final es obligatoria al componer: sin ella, Uri descarta el último segmento de
    /// la ruta y las peticiones salen a /api/token en la raíz del host en vez de bajo
    /// /mobicontrol. Antes lo resolvía BaseAddress, que ya no sirve porque cada empresa tiene
    /// una consola distinta y el HttpClient es compartido.
    private static Uri Ruta(ConfiguracionEmpresa config, string relativa) =>
        new(new Uri(config.MobiControlBaseUrl!.TrimEnd('/') + "/"), relativa);

    /// <summary>
    /// El asociado está esperando con el equipo en la mano: si la consola no responde, vale más
    /// cerrar el acta y reintentar la sincronización después que dejarlo colgado.
    /// </summary>
    private static CancellationTokenSource ConLimite(ConfiguracionEmpresa config, CancellationToken ct)
    {
        var fuente = CancellationTokenSource.CreateLinkedTokenSource(ct);
        fuente.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, config.MobiControlTimeoutSegundos)));
        return fuente;
    }

    private async Task<(string? Token, ResultadoIntegracion Resultado)> ObtenerTokenAsync(
        int empresaId, ConfiguracionEmpresa config, CancellationToken ct)
    {
        await Candado.WaitAsync(ct);
        try
        {
            if (Tokens.TryGetValue(empresaId, out var enCache) && DateTime.UtcNow < enCache.Expira)
                return (enCache.Token, new ResultadoIntegracion(TipoAccionIntegracion.ObtenerToken, true, null, "Token en caché."));

            using var limite = ConLimite(config, ct);
            using var peticion = new HttpRequestMessage(HttpMethod.Post, Ruta(config, "api/token"))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["username"] = config.MobiControlUsuario!,
                    ["password"] = config.MobiControlPassword!,
                }),
            };

            var credencial = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{config.MobiControlClientId}:{config.MobiControlClientSecret}"));
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Basic", credencial);

            using var respuesta = await http.SendAsync(peticion, limite.Token);
            var codigo = (int)respuesta.StatusCode;

            if (!respuesta.IsSuccessStatusCode)
            {
                var detalle = await LeerErrorAsync(respuesta, ct);
                logger.LogError("MobiControl rechazó el token de {Empresa} ({Codigo}): {Detalle}", config.TenantNombre, codigo, detalle);
                return (null, new ResultadoIntegracion(TipoAccionIntegracion.ObtenerToken, false, codigo, detalle));
            }

            var contenido = await respuesta.Content.ReadFromJsonAsync<RespuestaToken>(ct);
            if (string.IsNullOrWhiteSpace(contenido?.AccessToken))
                return (null, new ResultadoIntegracion(
                    TipoAccionIntegracion.ObtenerToken, false, codigo, "La respuesta no trajo access_token."));

            // Un minuto de colchón para no usar un token que caduca en pleno viaje.
            Tokens[empresaId] = new TokenEnCache(
                contenido.AccessToken,
                DateTime.UtcNow.AddSeconds(Math.Max(60, contenido.ExpiresIn) - 60));

            return (contenido.AccessToken, new ResultadoIntegracion(TipoAccionIntegracion.ObtenerToken, true, codigo, null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error obteniendo el token de MobiControl para {Empresa}.", config.TenantNombre);
            return (null, new ResultadoIntegracion(TipoAccionIntegracion.ObtenerToken, false, null, ex.Message));
        }
        finally
        {
            Candado.Release();
        }
    }

    private async Task<ResultadoIntegracion> ActualizarAtributosAsync(
        int empresaId, ConfiguracionEmpresa config, string token, string deviceId, DateOnly fechaEntrega,
        CancellationToken ct)
    {
        var cuerpo = new
        {
            Attributes = new object[]
            {
                new { AttributeName = config.MobiControlAtributoFirma, AttributeValue = (object)true },
                new { AttributeName = config.MobiControlAtributoFecha, AttributeValue = (object)fechaEntrega.ToString("yyyy-MM-dd") },
            },
        };

        return await EnviarAsync(
            empresaId, config, HttpMethod.Put, $"api/devices/{Uri.EscapeDataString(deviceId)}/customAttributes",
            cuerpo, token, TipoAccionIntegracion.ActualizarAtributos, ct);
    }

    private async Task<ResultadoIntegracion> CheckInAsync(
        int empresaId, ConfiguracionEmpresa config, string token, string deviceId, CancellationToken ct) =>
        await EnviarAsync(
            empresaId, config, HttpMethod.Post, $"api/devices/{Uri.EscapeDataString(deviceId)}/actions",
            new { Action = "CheckIn" }, token, TipoAccionIntegracion.CheckIn, ct);

    private async Task<ResultadoIntegracion> EnviarAsync(
        int empresaId, ConfiguracionEmpresa config, HttpMethod metodo, string ruta, object cuerpo,
        string token, string accion, CancellationToken ct)
    {
        try
        {
            using var limite = ConLimite(config, ct);
            using var peticion = new HttpRequestMessage(metodo, Ruta(config, ruta))
            {
                Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json"),
            };
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var respuesta = await http.SendAsync(peticion, limite.Token);
            var codigo = (int)respuesta.StatusCode;

            if (respuesta.IsSuccessStatusCode)
                return new ResultadoIntegracion(accion, true, codigo, null);

            // Un token revocado antes de tiempo se ve como 401: se descarta el de la caché para
            // que la siguiente acta vuelva a pedir uno en vez de repetir el mismo error.
            if (codigo == 401) Tokens.TryRemove(empresaId, out _);

            var detalle = await LeerErrorAsync(respuesta, ct);
            logger.LogError("MobiControl falló en {Accion} para {Empresa} ({Codigo}): {Detalle}", accion, config.TenantNombre, codigo, detalle);
            return new ResultadoIntegracion(accion, false, codigo, detalle);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error llamando a MobiControl en {Accion}.", accion);
            return new ResultadoIntegracion(accion, false, null, ex.Message);
        }
    }

    private static async Task<string> LeerErrorAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        var texto = await respuesta.Content.ReadAsStringAsync(ct);
        texto = texto.Trim();
        return texto.Length > 900 ? texto[..900] : texto;
    }

    private record RespuestaToken(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
