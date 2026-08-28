using MobiControlFirma.Domain.Enums;

namespace MobiControlFirma.Domain.Entities;

/// <summary>
/// Acta de entrega firmada. Es un histórico: cada firma agrega una fila y ninguna se
/// sobrescribe, porque el documento tiene valor probatorio frente a un descuento de nómina.
/// </summary>
public class EntregaDispositivo
{
    public int EntregaId { get; set; }

    /// <summary>Identificador público. Es el que viaja en las URLs del PDF y de la firma.</summary>
    public Guid EntregaUid { get; set; } = Guid.NewGuid();

    public int DispositivoId { get; set; }
    public Dispositivo Dispositivo { get; set; } = null!;

    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    // --- Copia congelada de los datos al momento de firmar -------------------------
    // Si mañana al asociado lo cambian de distrito o al equipo le rotan la SIM, el acta
    // debe seguir diciendo lo que decía el día que se firmó.
    public int? EstadoId { get; set; }
    public EstadoDispositivo? Estado { get; set; }

    public int? CanalId { get; set; }
    public Canal? Canal { get; set; }

    public int? DistritoId { get; set; }
    public Distrito? Distrito { get; set; }

    /// <summary>Nombre tal como el asociado lo escribió en el formulario antes de firmar.</summary>
    public string NombreAsociadoFirmante { get; set; } = string.Empty;

    public string? Entregables { get; set; }

    /// <summary>SIM instalada el día de la entrega (rota con cada cambio de línea).</summary>
    public string? ICCID { get; set; }

    /// <summary>Número asignado el día de la entrega.</summary>
    public string? NumeroCelular { get; set; }

    /// <summary>
    /// Costo del equipo al firmar. Es el valor sobre el que se calcula el descuento de la
    /// cláusula CUARTA, así que no puede leerse del catálogo años después.
    /// </summary>
    public decimal? CostoEquipo { get; set; }

    // -------------------------------------------------------------------------------

    public string CiudadFirma { get; set; } = "Cali";
    public DateOnly? FechaEntregaProgramada { get; set; }
    public DateTime FechaFirma { get; set; }

    public EstadoProceso EstadoProceso { get; set; } = EstadoProceso.FIRMADO;

    /// <summary>
    /// Marca que envía el formulario para poder reintentar sin duplicar. El dispositivo
    /// reintenta solo cuando la red falla, y sin esto una entrega se registraba dos veces.
    /// </summary>
    public string? ClaveIdempotencia { get; set; }

    public string? IPOrigen { get; set; }
    public string? UserAgent { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Firma? Firma { get; set; }
    public DocumentoPdf? DocumentoPdf { get; set; }
    public ICollection<IntegracionSincronizacion> Sincronizaciones { get; set; } = [];
}
