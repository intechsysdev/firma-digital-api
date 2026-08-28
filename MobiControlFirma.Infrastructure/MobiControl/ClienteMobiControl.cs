using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MobiControlFirma.Application.Common.Interfaces;
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
    IOptions<MobiControlOptions> opciones,
    ILogger<ClienteMobiControl> logger) : IClienteMobiControl
{
    private readonly MobiControlOptions _opciones = opciones.Value;

    // El token dura ~una hora y se reutiliza entre actas: pedir uno por firma multiplicaba por
    // tres las llamadas a la consola sin ninguna ganancia.
    private static readonly SemaphoreSlim Candado = new(1, 1);
    private static string? _token;
    private static DateTime _tokenExpira = DateTime.MinValue;

    public bool EstaConfigurado => _opciones.EstaConfigurado;

    public async Task<IReadOnlyList<ResultadoIntegracion>> MarcarEntregaFirmadaAsync(
        string deviceId, DateOnly fechaEntrega, CancellationToken ct = default)
    {
        var resultados = new List<ResultadoIntegracion>();

        if (!EstaConfigurado)
        {
            resultados.Add(new ResultadoIntegracion(
                TipoAccionIntegracion.ObtenerToken, false, null,
                "MobiControl no está configurado en el API (sección 'MobiControl')."));
            return resultados;
        }

        var (token, resultadoToken) = await ObtenerTokenAsync(ct);
        resultados.Add(resultadoToken);
        if (token is null) return resultados;

        resultados.Add(await ActualizarAtributosAsync(token, deviceId, fechaEntrega, ct));

        // El check-in se pide aunque la actualización de atributos haya fallado: es barato y,
        // si el fallo fue de red y no de datos, deja el equipo reportando igual.
        resultados.Add(await CheckInAsync(token, deviceId, ct));

        return resultados;
    }

    private async Task<(string? Token, ResultadoIntegracion Resultado)> ObtenerTokenAsync(CancellationToken ct)
    {
        await Candado.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow < _tokenExpira)
                return (_token, new ResultadoIntegracion(TipoAccionIntegracion.ObtenerToken, true, null, "Token en caché."));

            using var peticion = new HttpRequestMessage(HttpMethod.Post, "api/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["username"] = _opciones.Usuario,
                    ["password"] = _opciones.Password,
                }),
            };

            var credencial = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_opciones.ClientId}:{_opciones.ClientSecret}"));
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Basic", credencial);

            using var respuesta = await http.SendAsync(peticion, ct);
            var codigo = (int)respuesta.StatusCode;

            if (!respuesta.IsSuccessStatusCode)
            {
                var detalle = await LeerErrorAsync(respuesta, ct);
                logger.LogError("MobiControl rechazó el token ({Codigo}): {Detalle}", codigo, detalle);
                return (null, new ResultadoIntegracion(TipoAccionIntegracion.ObtenerToken, false, codigo, detalle));
            }

            var contenido = await respuesta.Content.ReadFromJsonAsync<RespuestaToken>(ct);
            if (string.IsNullOrWhiteSpace(contenido?.AccessToken))
                return (null, new ResultadoIntegracion(
                    TipoAccionIntegracion.ObtenerToken, false, codigo, "La respuesta no trajo access_token."));

            _token = contenido.AccessToken;
            // Un minuto de colchón para no usar un token que caduca en pleno viaje.
            _tokenExpira = DateTime.UtcNow.AddSeconds(Math.Max(60, contenido.ExpiresIn) - 60);

            return (_token, new ResultadoIntegracion(TipoAccionIntegracion.ObtenerToken, true, codigo, null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error obteniendo el token de MobiControl.");
            return (null, new ResultadoIntegracion(TipoAccionIntegracion.ObtenerToken, false, null, ex.Message));
        }
        finally
        {
            Candado.Release();
        }
    }

    private async Task<ResultadoIntegracion> ActualizarAtributosAsync(
        string token, string deviceId, DateOnly fechaEntrega, CancellationToken ct)
    {
        var cuerpo = new
        {
            Attributes = new object[]
            {
                new { AttributeName = _opciones.AtributoFirma, AttributeValue = (object)true },
                new { AttributeName = _opciones.AtributoFecha, AttributeValue = (object)fechaEntrega.ToString("yyyy-MM-dd") },
            },
        };

        return await EnviarAsync(
            HttpMethod.Put, $"api/devices/{Uri.EscapeDataString(deviceId)}/customAttributes",
            cuerpo, token, TipoAccionIntegracion.ActualizarAtributos, ct);
    }

    private async Task<ResultadoIntegracion> CheckInAsync(string token, string deviceId, CancellationToken ct) =>
        await EnviarAsync(
            HttpMethod.Post, $"api/devices/{Uri.EscapeDataString(deviceId)}/actions",
            new { Action = "CheckIn" }, token, TipoAccionIntegracion.CheckIn, ct);

    private async Task<ResultadoIntegracion> EnviarAsync(
        HttpMethod metodo, string ruta, object cuerpo, string token, string accion, CancellationToken ct)
    {
        try
        {
            using var peticion = new HttpRequestMessage(metodo, ruta)
            {
                Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json"),
            };
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var respuesta = await http.SendAsync(peticion, ct);
            var codigo = (int)respuesta.StatusCode;

            if (respuesta.IsSuccessStatusCode)
                return new ResultadoIntegracion(accion, true, codigo, null);

            // Un token revocado antes de tiempo se ve como 401: se descarta el de la caché para
            // que la siguiente acta vuelva a pedir uno en vez de repetir el mismo error.
            if (codigo == 401) _tokenExpira = DateTime.MinValue;

            var detalle = await LeerErrorAsync(respuesta, ct);
            logger.LogError("MobiControl falló en {Accion} ({Codigo}): {Detalle}", accion, codigo, detalle);
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
