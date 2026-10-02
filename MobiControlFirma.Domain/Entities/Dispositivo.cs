namespace MobiControlFirma.Domain.Entities;

/// <summary>
/// Equipo entregado. Si está en MobiControl, su identidad es <see cref="MobiControlDeviceId"/>
/// (el <c>%deviceid%</c> del formulario). Las solicitudes por enlace pueden traer solo el IMEI o
/// el serial —un PC, por ejemplo, puede no estar en la consola—, y entonces el equipo se reconoce
/// por esos datos y el acta se firma sin marcarlo en MobiControl.
/// </summary>
public class Dispositivo : IDeEmpresa
{
    public int DispositivoId { get; set; }

    /// <summary>Empresa dueña del registro. El contexto filtra por aquí en cada consulta.</summary>
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    /// <summary>Null si el equipo no está (o todavía no se encontró) en MobiControl.</summary>
    public string? MobiControlDeviceId { get; set; }

    /// <summary>Celular, Tableta, PC… Texto libre: el proceso puede sumar tipos de equipo.</summary>
    public string? TipoDispositivo { get; set; }

    /// <summary>Serial del fabricante. Identifica equipos sin IMEI, como un PC.</summary>
    public string? Serial { get; set; }

    public string? Fabricante { get; set; }
    public string? Modelo { get; set; }
    public string? IMEI { get; set; }
    public string? ICCID { get; set; }
    public string? NumeroCelular { get; set; }
    public decimal? CostoEquipo { get; set; }

    public int? EstadoActualId { get; set; }
    public EstadoDispositivo? EstadoActual { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }

    public ICollection<EntregaDispositivo> Entregas { get; set; } = [];
}
