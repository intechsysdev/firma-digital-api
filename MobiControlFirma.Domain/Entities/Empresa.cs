namespace MobiControlFirma.Domain.Entities;

/// <summary>
/// Vínculo con un tenant de One. Ya no es dueña de ninguna configuración: las credenciales de
/// MobiControl, las de Infobip y los destinatarios de copia viven en One como variables de la
/// app "firma-digital", y se piden a su API de integración.
///
/// Aquí solo queda lo que hace falta antes de poder hablar con One: a qué tenant pertenece cada
/// fila de este sistema, qué llave llevan sus equipos, y con qué credencial preguntarle a One.
/// La llave del dispositivo no puede vivir allá porque es justamente lo que se usa para saber
/// de qué tenant es la petición que acaba de llegar.
/// </summary>
public class Empresa
{
    public int EmpresaId { get; set; }

    // ---- Identidad en One ----

    /// <summary>Tenant de One dueño de estos datos. Es el vínculo con el registro central.</summary>
    public Guid OneTenantId { get; set; }

    /// <summary>Slug del tenant, copiado de One para poder mostrarlo sin ir a preguntar.</summary>
    public string OneSlug { get; set; } = string.Empty;

    /// <summary>Nombre del tenant, copia de conveniencia. La fuente de verdad es One.</summary>
    public string Nombre { get; set; } = string.Empty;

    // ---- Credencial para consultar la configuración en One ----

    /// <summary>Api key emitida por One para el par tenant–firma-digital.</summary>
    public string? OneApiKey { get; set; }

    /// <summary>Secreto de esa credencial. Nunca sale de este servidor.</summary>
    public string? OneApiSecret { get; set; }

    /// <summary>Sin credencial no hay configuración: las actas se firman pero no se sincronizan.</summary>
    public bool OneConfigurado =>
        !string.IsNullOrWhiteSpace(OneApiKey) && !string.IsNullOrWhiteSpace(OneApiSecret);

    // ---- Llave que llevan los equipos ----

    /// <summary>
    /// SHA-256 de la llave instalada en los equipos de este tenant. Se guarda el resumen y no la
    /// llave: quien lea la base no puede registrar actas a nombre de la empresa.
    /// </summary>
    public byte[] ApiKeyHash { get; set; } = [];

    /// <summary>Primeros caracteres, en claro, para reconocer cuál está instalada.</summary>
    public string ApiKeyPrefijo { get; set; } = string.Empty;

    public DateTime? ApiKeyRotadaEn { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}

/// <summary>Contrato de lo que pertenece a una empresa. Lo usa el filtro global del contexto.</summary>
public interface IDeEmpresa
{
    int EmpresaId { get; set; }
    Empresa Empresa { get; set; }
}
