namespace MobiControlFirma.Domain.Entities;

/// <summary>
/// Cliente del sistema. Es la raíz del aislamiento: todo lo que se registra —actas, equipos,
/// asociados y catálogos— pertenece a una empresa y nunca se cruza con el de otra.
/// </summary>
public class Empresa
{
    public int EmpresaId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Identificación tributaria. Sirve para distinguir dos empresas de nombre parecido.</summary>
    public string? Nit { get; set; }

    /// <summary>Ciudad que sale impresa en el acta cuando el formulario no manda otra.</summary>
    public string CiudadFirma { get; set; } = "Cali";

    public bool Activo { get; set; } = true;

    // ---- Llave del formulario instalado en los equipos ----

    /// <summary>
    /// SHA-256 de la llave que llevan los equipos de esta empresa. Se guarda el resumen y no la
    /// llave: quien lea la base no puede registrar actas a nombre de la empresa. La búsqueda
    /// funciona igual porque el API calcula el resumen de lo que llega y compara por índice.
    /// </summary>
    public byte[] ApiKeyHash { get; set; } = [];

    /// <summary>
    /// Primeros caracteres de la llave, en claro. Es lo único que la consola puede mostrar para
    /// que un administrador reconozca cuál está instalada sin poder reconstruirla.
    /// </summary>
    public string ApiKeyPrefijo { get; set; } = string.Empty;

    public DateTime? ApiKeyRotadaEn { get; set; }

    // ---- Consola de MobiControl propia de la empresa ----

    public string? MobiControlBaseUrl { get; set; }
    public string? MobiControlClientId { get; set; }
    public string? MobiControlClientSecret { get; set; }
    public string? MobiControlUsuario { get; set; }
    public string? MobiControlPassword { get; set; }

    public string MobiControlAtributoFirma { get; set; } = "Firma de entrega";
    public string MobiControlAtributoFecha { get; set; } = "Fecha de entrega";
    public int MobiControlTimeoutSegundos { get; set; } = 20;

    /// <summary>Sin consola configurada las actas se guardan igual, pero quedan sin sincronizar.</summary>
    public bool MobiControlConfigurado =>
        !string.IsNullOrWhiteSpace(MobiControlBaseUrl) &&
        !string.IsNullOrWhiteSpace(MobiControlClientId) &&
        !string.IsNullOrWhiteSpace(MobiControlClientSecret) &&
        !string.IsNullOrWhiteSpace(MobiControlUsuario) &&
        !string.IsNullOrWhiteSpace(MobiControlPassword);

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}

/// <summary>Contrato de lo que pertenece a una empresa. Lo usa el filtro global del contexto.</summary>
public interface IDeEmpresa
{
    int EmpresaId { get; set; }
    Empresa Empresa { get; set; }
}
