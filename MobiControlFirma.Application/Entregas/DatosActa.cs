namespace MobiControlFirma.Application.Entregas;

/// <summary>
/// Todo lo que el acta necesita imprimir, ya normalizado. Se arma en el API para que el
/// generador de PDF no dependa de las entidades ni de la base de datos.
/// </summary>
public record DatosActa
{
    public required Guid EntregaUid { get; init; }
    public required string NombreTenedor { get; init; }
    public required string Cedula { get; init; }
    public required string NombreAsociadoFirmante { get; init; }

    public string? Fabricante { get; init; }
    public string? Modelo { get; init; }
    public string? Imei { get; init; }
    public string? Iccid { get; init; }
    public string? NumeroCelular { get; init; }
    public string? Estado { get; init; }
    public string? Canal { get; init; }
    public string? Distrito { get; init; }
    public decimal? CostoEquipo { get; init; }
    public string? Entregables { get; init; }

    public required string CiudadFirma { get; init; }
    public required DateTime FechaFirma { get; init; }
    public string? DeviceId { get; init; }
}
