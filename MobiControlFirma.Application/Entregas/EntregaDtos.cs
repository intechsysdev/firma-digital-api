using System.ComponentModel.DataAnnotations;

namespace MobiControlFirma.Application.Entregas;

/// <summary>
/// Lo que envía el formulario del dispositivo. Casi todos los campos llegan resueltos por
/// MobiControl (<c>%MANUFACTURER%</c>, <c>%CustomAttr:Cedula%</c>, …); por eso viajan como
/// texto libre y el API los normaliza en vez de rechazarlos.
/// </summary>
public class RegistrarEntregaRequest
{
    /// <summary>Valor de <c>%deviceid%</c>. Es la identidad del equipo en MobiControl.</summary>
    [Required, MaxLength(100)]
    public string DeviceId { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Cedula { get; set; } = string.Empty;

    /// <summary>Nombre que trae el atributo personalizado <c>Usuario</c>.</summary>
    [MaxLength(200)]
    public string? Usuario { get; set; }

    /// <summary>Nombre que el asociado confirmó o corrigió en el formulario antes de firmar.</summary>
    [Required, MaxLength(200)]
    public string NombreAsociado { get; set; } = string.Empty;

    [MaxLength(100)] public string? Fabricante { get; set; }
    [MaxLength(100)] public string? Modelo { get; set; }
    [MaxLength(50)]  public string? Imei { get; set; }
    [MaxLength(50)]  public string? Iccid { get; set; }
    [MaxLength(30)]  public string? NumeroCelular { get; set; }
    [MaxLength(50)]  public string? Estado { get; set; }
    [MaxLength(100)] public string? Canal { get; set; }
    [MaxLength(100)] public string? Distrito { get; set; }

    /// <summary>Costo tal como se muestra en el acta ("$ 1.200.000", "1200000"…).</summary>
    [MaxLength(50)]
    public string? Costo { get; set; }

    public string? Entregables { get; set; }

    [MaxLength(100)] public string CiudadFirma { get; set; } = "Cali";

    /// <summary>Si no viene, el API usa el día siguiente (lo mismo que hacía el formulario).</summary>
    public DateOnly? FechaEntregaProgramada { get; set; }

    /// <summary>Firma en PNG: data URL (<c>data:image/png;base64,…</c>) o base64 puro.</summary>
    [Required]
    public string FirmaBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Identificador que genera el formulario y repite en cada reintento. Permite volver a
    /// enviar un acta cuando se cayó la red sin que quede registrada dos veces.
    /// </summary>
    [MaxLength(100)]
    public string? ClaveIdempotencia { get; set; }

    /// <summary>Se puede apagar para pruebas: guarda el acta sin tocar MobiControl.</summary>
    public bool SincronizarMobiControl { get; set; } = true;
}

/// <summary>Respuesta al registrar: lo mínimo que el formulario necesita para cerrar el flujo.</summary>
public record EntregaCreadaResponse(
    Guid EntregaUid,
    int EntregaId,
    DateTime FechaFirma,
    string EstadoProceso,
    string UrlPdf,
    string UrlFirma,
    bool Duplicada,
    IReadOnlyList<ResultadoSincronizacionDto> Sincronizacion);

/// <summary>Detalle de una llamada a un sistema externo, para diagnóstico desde el cliente.</summary>
public record ResultadoSincronizacionDto(string Proveedor, string Accion, bool Exitoso, int? CodigoHttp, string? Mensaje);

/// <summary>Fila del listado de actas (equivale a la vista vw_EntregasCompletas).</summary>
public record EntregaResumenDto(
    Guid EntregaUid,
    int EntregaId,
    string DeviceId,
    string? Fabricante,
    string? Modelo,
    string? Imei,
    string Cedula,
    string NombreCompleto,
    string NombreAsociadoFirmante,
    string? Estado,
    string? Canal,
    string? Distrito,
    DateTime FechaFirma,
    string EstadoProceso,
    string UrlPdf,
    string UrlFirma);

/// <summary>Resultado paginado del listado de actas.</summary>
public record PaginaDto<T>(IReadOnlyList<T> Items, int Total, int Pagina, int TamanoPagina);

/// <summary>
/// Estado de firma de un equipo. El formulario lo consulta al abrir para avisar que el acta
/// ya existe, en vez de dejar que el asociado firme de nuevo sin darse cuenta.
/// </summary>
public record EstadoFirmaDispositivoDto(
    string DeviceId,
    bool YaFirmado,
    Guid? EntregaUid,
    DateTime? FechaFirma,
    string? NombreAsociadoFirmante,
    string? UrlPdf);
