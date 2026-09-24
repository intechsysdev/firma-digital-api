using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using MobiControlFirma.Application.Common.Interfaces;

namespace MobiControlFirma.API.Configuration;

/// <summary>
/// Enlaces de firma cifrados con Data Protection. Así no se guarda ningún secreto en la base:
/// quien la lea ve los identificadores de las solicitudes, pero no puede armar un enlace con
/// ellos. Y como el token se puede volver a emitir cuando se quiera, un origen que repite la
/// solicitud recibe un enlace nuevo sin invalidar el que ya se había mandado.
///
/// Las llaves las guarda el propio App Service en %HOME%, compartidas entre instancias. Si se
/// perdieran, los enlaces pendientes dejarían de abrir; basta con que el origen vuelva a pedir
/// la solicitud para recibir uno válido.
/// </summary>
public class EnlacesFirma(IDataProtectionProvider proveedor, IOptions<AppOptions> opciones) : IEnlacesFirma
{
    /// <summary>
    /// Propósito del cifrado. Aísla estos tokens de cualquier otro uso de Data Protection: un
    /// valor cifrado para otra cosa no se puede presentar aquí como enlace de firma.
    /// </summary>
    private readonly IDataProtector protector = proveedor.CreateProtector("MobiControlFirma.EnlacesFirma.v1");

    public string UrlParaFirmar(Guid solicitudUid)
    {
        var raiz = opciones.Value.UrlFront?.Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(raiz))
            throw new InvalidOperationException(
                "Falta 'App:UrlFront': sin la dirección del front no se puede armar el enlace de firma.");

        var token = WebEncoders.Base64UrlEncode(protector.Protect(solicitudUid.ToByteArray()));
        return $"{raiz}/firmar/{token}";
    }

    public Guid? LeerToken(string token)
    {
        try
        {
            var bytes = protector.Unprotect(WebEncoders.Base64UrlDecode(token));
            return bytes.Length == 16 ? new Guid(bytes) : null;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Un enlace recortado al copiarlo o inventado: para quien llama es simplemente
            // un enlace que no sirve.
            return null;
        }
    }
}
