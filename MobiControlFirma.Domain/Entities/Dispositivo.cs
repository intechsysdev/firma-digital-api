namespace MobiControlFirma.Domain.Entities;

/// <summary>
/// Equipo administrado en MobiControl. La identidad es <see cref="MobiControlDeviceId"/>
/// (el <c>%deviceid%</c> del formulario): el IMEI puede venir vacío en equipos que aún no
/// reportan y no sirve como llave.
/// </summary>
public class Dispositivo
{
    public int DispositivoId { get; set; }
    public string MobiControlDeviceId { get; set; } = string.Empty;

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
