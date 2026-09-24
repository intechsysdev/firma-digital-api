namespace MobiControlFirma.Application.Common.Interfaces;

/// <summary>
/// Arma y lee los enlaces de firma. El enlace es la única credencial de quien firma —no tiene
/// usuario ni llave—, así que lleva la solicitud cifrada en vez de su identificador en claro.
/// </summary>
public interface IEnlacesFirma
{
    /// <summary>URL completa del formulario web para una solicitud.</summary>
    string UrlParaFirmar(Guid solicitudUid);

    /// <summary>Solicitud a la que apunta un token, o null si no es uno emitido por este sistema.</summary>
    Guid? LeerToken(string token);
}
