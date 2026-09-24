namespace MobiControlFirma.API.Configuration;

/// <summary>
/// Conexión con One, el centralizador de empresas y configuración.
///
/// La llave de firma es la misma con la que One firma sus tokens: este API no emite
/// credenciales propias, solo verifica las que emitió One. Va por configuración de entorno y
/// nunca en el repositorio.
/// </summary>
public class OneOptions
{
    public const string SectionName = "One";

    /// <summary>Raíz del API de One, para consultar la configuración de cada empresa.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string Issuer { get; set; } = "one-api";
    public string Audience { get; set; } = "one-front";

    /// <summary>Secreto compartido con el que One firma sus tokens (HMAC-SHA256).</summary>
    public string SigningKey { get; set; } = string.Empty;

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(SigningKey);
}

/// <summary>Claims y cabeceras del protocolo de One que este API entiende.</summary>
public static class One
{
    /// <summary>Pertenencia a una empresa, con formato "{tenantId}:{rol}".</summary>
    public const string ClaimTenant = "tenant";

    /// <summary>Rol de plataforma que ve todas las empresas.</summary>
    public const string RolPlataforma = "PlatformAdmin";

    /// <summary>
    /// Empresa sobre la que trabaja la petición. Un usuario puede pertenecer a varias, así que
    /// la consola indica cuál tiene abierta; sin cabecera se usa la única que tenga.
    /// </summary>
    public const string CabeceraTenant = "X-Tenant-Id";
}
