namespace MobiControlFirma.Domain.Entities;

/// <summary>
/// Persona que recibe el equipo (EL TENEDOR en el acta). Se identifica por cédula porque es
/// el único dato que MobiControl envía de forma estable en <c>%CustomAttr:Cedula%</c>.
/// </summary>
public class Empleado
{
    public int EmpleadoId { get; set; }
    public string Cedula { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>Canal y distrito actuales. El histórico de cada entrega guarda su propia copia.</summary>
    public int? CanalId { get; set; }
    public Canal? Canal { get; set; }

    public int? DistritoId { get; set; }
    public Distrito? Distrito { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }

    public ICollection<EntregaDispositivo> Entregas { get; set; } = [];
}
