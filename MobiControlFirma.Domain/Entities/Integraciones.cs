using MobiControlFirma.Domain.Enums;

namespace MobiControlFirma.Domain.Entities;

/// <summary>
/// Bitácora de cada llamada a un sistema externo. Es lo que permite responder "¿por qué este
/// equipo sigue sin la marca de firma en MobiControl?" sin tener que leer logs del servidor.
/// </summary>
public class IntegracionSincronizacion
{
    public int SincronizacionId { get; set; }

    public int EntregaId { get; set; }
    public EntregaDispositivo Entrega { get; set; } = null!;

    public ProveedorIntegracion Proveedor { get; set; }

    /// <summary>Ver <see cref="TipoAccionIntegracion"/>.</summary>
    public string TipoAccion { get; set; } = string.Empty;

    public bool Exitoso { get; set; }
    public int? CodigoRespuestaHttp { get; set; }
    public string? MensajeError { get; set; }
    public DateTime FechaEjecucion { get; set; }
}

/// <summary>
/// Configuración por proveedor y entorno. Las credenciales se guardan cifradas o, mejor,
/// no se guardan: el API las lee de la configuración de la aplicación y usa esta tabla solo
/// para saber a qué URL apuntar y qué proveedores están habilitados.
/// </summary>
public class IntegracionConfiguracion
{
    public int ConfiguracionId { get; set; }
    public ProveedorIntegracion Proveedor { get; set; }
    public string Entorno { get; set; } = "PRODUCCION";
    public TipoAutenticacion TipoAutenticacion { get; set; }
    public string UrlBase { get; set; } = string.Empty;

    public byte[]? ClientIdCifrado { get; set; }
    public byte[]? ClientSecretCifrado { get; set; }
    public byte[]? UsuarioCifrado { get; set; }
    public byte[]? PasswordCifrado { get; set; }
    public byte[]? ApiKeyCifrada { get; set; }

    /// <summary>JSON con lo propio de cada proveedor (remitente de Infobip, app de Gupshup…).</summary>
    public string? ParametrosAdicionales { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
