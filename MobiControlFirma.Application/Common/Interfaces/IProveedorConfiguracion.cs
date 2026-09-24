namespace MobiControlFirma.Application.Common.Interfaces;

/// <summary>
/// Configuración de una empresa, resuelta desde One. Las claves son el contrato con el catálogo
/// de apps: cambiar un nombre aquí obliga a cambiarlo en el esquema de la app en One.
/// </summary>
public sealed record ConfiguracionEmpresa(
    string TenantSlug,
    string TenantNombre,
    string? MobiControlBaseUrl,
    string? MobiControlClientId,
    string? MobiControlClientSecret,
    string? MobiControlUsuario,
    string? MobiControlPassword,
    string MobiControlAtributoFirma,
    string MobiControlAtributoFecha,
    int MobiControlTimeoutSegundos,
    string? CorreosCopia,
    string? InfobipBaseUrl,
    string? InfobipApiKey,
    string? InfobipRemitente,
    string? InfobipNombreRemitente,
    string CiudadFirma,
    string? CallbackUrl,
    string? CallbackSecreto,
    string ConfigVersion)
{
    /// <summary>Sin consola configurada las actas se firman igual, solo quedan sin sincronizar.</summary>
    public bool MobiControlConfigurado =>
        !string.IsNullOrWhiteSpace(MobiControlBaseUrl) &&
        !string.IsNullOrWhiteSpace(MobiControlClientId) &&
        !string.IsNullOrWhiteSpace(MobiControlClientSecret) &&
        !string.IsNullOrWhiteSpace(MobiControlUsuario) &&
        !string.IsNullOrWhiteSpace(MobiControlPassword);

    public bool CorreoConfigurado =>
        !string.IsNullOrWhiteSpace(InfobipBaseUrl) &&
        !string.IsNullOrWhiteSpace(InfobipApiKey) &&
        !string.IsNullOrWhiteSpace(InfobipRemitente);

    /// <summary>
    /// A dónde avisar que una solicitud de firma se completó. Sin esto las firmas por enlace se
    /// registran igual; el aviso queda en la bandeja reintentándose hasta que se configure.
    /// </summary>
    public bool CallbackConfigurado =>
        Uri.TryCreate(CallbackUrl, UriKind.Absolute, out var url) &&
        (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp);
}

/// <summary>Resuelve la configuración de una empresa preguntándole a One.</summary>
public interface IProveedorConfiguracion
{
    /// <summary>
    /// Null cuando la empresa no tiene credencial de One o cuando One la rechaza. El API sigue
    /// funcionando en ese caso: guarda el acta y deja la sincronización pendiente.
    /// </summary>
    Task<ConfiguracionEmpresa?> ObtenerAsync(int empresaId, CancellationToken ct = default);

    /// <summary>Descarta lo cacheado de una empresa. Se usa tras cambiar su credencial.</summary>
    void Olvidar(int empresaId);
}
