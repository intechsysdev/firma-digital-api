using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Domain.Entities;

namespace MobiControlFirma.Infrastructure.Correo;

/// <summary>
/// Envía los correos de la empresa (la copia del acta, el enlace para firmar) por la API de correo
/// de Infobip: <c>POST {base}/email/3/send</c> en multipart, con <c>Authorization: App {llave}</c>.
///
/// Cada empresa trae su propia cuenta: la URL base de Infobip es específica de cada cliente y
/// el remitente tiene que estar verificado en esa cuenta, así que no hay forma de compartir una
/// sola configuración entre empresas.
/// </summary>
public class EnviadorCorreoInfobip(HttpClient http, ILogger<EnviadorCorreoInfobip> logger) : IEnviadorCorreo
{
    public Task<ResultadoCorreo> EnviarActaAsync(
        ConfiguracionEmpresa config,
        IReadOnlyList<string> destinatarios,
        string asunto,
        string cuerpoHtml,
        string nombreArchivo,
        byte[] pdf,
        CancellationToken ct = default) =>
        EnviarConAdjuntoAsync(config, destinatarios, asunto, cuerpoHtml, (nombreArchivo, pdf), ct);

    public Task<ResultadoCorreo> EnviarAsync(
        ConfiguracionEmpresa config,
        IReadOnlyList<string> destinatarios,
        string asunto,
        string cuerpoHtml,
        CancellationToken ct = default) =>
        EnviarConAdjuntoAsync(config, destinatarios, asunto, cuerpoHtml, null, ct);

    private async Task<ResultadoCorreo> EnviarConAdjuntoAsync(
        ConfiguracionEmpresa config,
        IReadOnlyList<string> destinatarios,
        string asunto,
        string cuerpoHtml,
        (string Nombre, byte[] Pdf)? adjunto,
        CancellationToken ct)
    {
        if (!config.CorreoConfigurado)
            return new ResultadoCorreo(false, null,
                $"El tenant {config.TenantNombre} no tiene configurado el envío de correos en One.");

        if (destinatarios.Count == 0)
            return new ResultadoCorreo(false, null, "No hay destinatarios.");

        try
        {
            var baseUrl = config.InfobipBaseUrl!.TrimEnd('/');

            using var contenido = new MultipartFormDataContent
            {
                { new StringContent(Remitente(config)), "from" },
                { new StringContent(asunto), "subject" },
                { new StringContent(cuerpoHtml), "html" },
                // La versión en texto: la leen los clientes que no muestran HTML y pesa a favor
                // en los filtros de spam.
                { new StringContent(TextoPlano(cuerpoHtml)), "text" },
                { new StringContent(config.InfobipSeguimiento ? "true" : "false"), "trackOpens" },
                { new StringContent(config.InfobipSeguimiento ? "true" : "false"), "trackClicks" },
            };

            // Las respuestas del asociado van a quien las atiende, no al buzón que solo envía.
            if (!string.IsNullOrWhiteSpace(config.InfobipResponderA))
                contenido.Add(new StringContent(config.InfobipResponderA), "replyTo");

            // Un campo "to" por destinatario: así cada uno recibe el acta sin ver la lista de
            // los demás, que en este caso incluye el correo personal del asociado.
            foreach (var destino in destinatarios)
                contenido.Add(new StringContent(destino), "to");

            if (adjunto is { } archivo)
            {
                var pdf = new ByteArrayContent(archivo.Pdf);
                pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                contenido.Add(pdf, "attachment", archivo.Nombre);
            }

            using var peticion = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/email/3/send")
            {
                Content = contenido,
            };
            peticion.Headers.Authorization = new AuthenticationHeaderValue("App", Llave(config.InfobipApiKey!));

            using var respuesta = await http.SendAsync(peticion, ct);
            var codigo = (int)respuesta.StatusCode;

            if (respuesta.IsSuccessStatusCode)
                return new ResultadoCorreo(true, codigo, null);

            var detalle = await LeerErrorAsync(respuesta, ct);
            logger.LogError("Infobip rechazó el correo de {Empresa} ({Codigo}): {Detalle}",
                config.TenantNombre, codigo, detalle);

            return new ResultadoCorreo(false, codigo, detalle);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enviando un correo de {Empresa}.", config.TenantNombre);
            return new ResultadoCorreo(false, null, ex.Message);
        }
    }

    /// <summary>Con nombre si lo hay: "Actas Intechsys &lt;actas@…&gt;" se lee mejor en la bandeja.</summary>
    private static string Remitente(ConfiguracionEmpresa config) =>
        string.IsNullOrWhiteSpace(config.InfobipNombreRemitente)
            ? config.InfobipRemitente!
            : $"{config.InfobipNombreRemitente} <{config.InfobipRemitente}>";

    /// <summary>
    /// La llave sola. Si en One se pegó con el prefijo ("App xxxx"), como se ve en los ejemplos de
    /// Infobip, se quita: la cabecera ya lo lleva y repetido Infobip responde 401.
    /// </summary>
    internal static string Llave(string valor)
    {
        var llave = valor.Trim();
        return llave.StartsWith("App ", StringComparison.OrdinalIgnoreCase) ? llave[4..].Trim() : llave;
    }

    /// <summary>El cuerpo HTML como texto: los enlaces quedan como "texto (url)" y los párrafos en líneas.</summary>
    internal static string TextoPlano(string html)
    {
        var texto = Regex.Replace(html, @"<a\s[^>]*href=""([^""]*)""[^>]*>(.*?)</a>", m =>
        {
            var url = WebUtility.HtmlDecode(m.Groups[1].Value);
            var etiqueta = Regex.Replace(m.Groups[2].Value, "<[^>]+>", "").Trim();
            return etiqueta.Length == 0 || WebUtility.HtmlDecode(etiqueta) == url ? url : $"{etiqueta} ({url})";
        }, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        texto = Regex.Replace(texto, @"<(style|script)[^>]*>.*?</\1>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        texto = Regex.Replace(texto, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto, @"</(p|div|h[1-6]|li|tr|table)>", "\n\n", RegexOptions.IgnoreCase);
        texto = WebUtility.HtmlDecode(Regex.Replace(texto, "<[^>]+>", ""));

        var lineas = texto.Split('\n').Select(l => Regex.Replace(l, @"[ \t]+", " ").Trim());
        return Regex.Replace(string.Join("\n", lineas), @"\n{3,}", "\n\n").Trim();
    }

    private static async Task<string> LeerErrorAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        var texto = await respuesta.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(texto)
            ? respuesta.ReasonPhrase ?? "Sin detalle."
            : texto[..Math.Min(900, texto.Length)];
    }
}
