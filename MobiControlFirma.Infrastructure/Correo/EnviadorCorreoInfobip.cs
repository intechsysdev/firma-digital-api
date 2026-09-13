using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Domain.Entities;

namespace MobiControlFirma.Infrastructure.Correo;

/// <summary>
/// Envía la copia del acta por la API de correo de Infobip.
///
/// Cada empresa trae su propia cuenta: la URL base de Infobip es específica de cada cliente y
/// el remitente tiene que estar verificado en esa cuenta, así que no hay forma de compartir una
/// sola configuración entre empresas.
/// </summary>
public class EnviadorCorreoInfobip(HttpClient http, ILogger<EnviadorCorreoInfobip> logger) : IEnviadorCorreo
{
    public async Task<ResultadoCorreo> EnviarActaAsync(
        Empresa empresa,
        IReadOnlyList<string> destinatarios,
        string asunto,
        string cuerpoHtml,
        string nombreArchivo,
        byte[] pdf,
        CancellationToken ct = default)
    {
        if (!empresa.CorreoConfigurado)
            return new ResultadoCorreo(false, null, $"La empresa {empresa.Nombre} no tiene configurado el envío de correos.");

        if (destinatarios.Count == 0)
            return new ResultadoCorreo(false, null, "No hay destinatarios.");

        try
        {
            var baseUrl = empresa.InfobipBaseUrl!.TrimEnd('/');

            using var contenido = new MultipartFormDataContent
            {
                { new StringContent(Remitente(empresa)), "from" },
                { new StringContent(asunto), "subject" },
                { new StringContent(cuerpoHtml), "html" },
            };

            // Un campo "to" por destinatario: así cada uno recibe el acta sin ver la lista de
            // los demás, que en este caso incluye el correo personal del asociado.
            foreach (var destino in destinatarios)
                contenido.Add(new StringContent(destino), "to");

            var adjunto = new ByteArrayContent(pdf);
            adjunto.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            contenido.Add(adjunto, "attachment", nombreArchivo);

            using var peticion = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/email/3/send")
            {
                Content = contenido,
            };
            peticion.Headers.Authorization = new AuthenticationHeaderValue("App", empresa.InfobipApiKey);

            using var respuesta = await http.SendAsync(peticion, ct);
            var codigo = (int)respuesta.StatusCode;

            if (respuesta.IsSuccessStatusCode)
                return new ResultadoCorreo(true, codigo, null);

            var detalle = await LeerErrorAsync(respuesta, ct);
            logger.LogError("Infobip rechazó el correo de {Empresa} ({Codigo}): {Detalle}",
                empresa.Nombre, codigo, detalle);

            return new ResultadoCorreo(false, codigo, detalle);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enviando la copia del acta de {Empresa}.", empresa.Nombre);
            return new ResultadoCorreo(false, null, ex.Message);
        }
    }

    /// <summary>Con nombre si lo hay: "Actas Intechsys &lt;actas@…&gt;" se lee mejor en la bandeja.</summary>
    private static string Remitente(Empresa empresa) =>
        string.IsNullOrWhiteSpace(empresa.InfobipNombreRemitente)
            ? empresa.InfobipRemitente!
            : $"{empresa.InfobipNombreRemitente} <{empresa.InfobipRemitente}>";

    private static async Task<string> LeerErrorAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        var texto = await respuesta.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(texto)
            ? respuesta.ReasonPhrase ?? "Sin detalle."
            : texto[..Math.Min(900, texto.Length)];
    }
}
