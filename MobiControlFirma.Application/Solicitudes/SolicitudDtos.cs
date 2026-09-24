using System.ComponentModel.DataAnnotations;

namespace MobiControlFirma.Application.Solicitudes;

/// <summary>
/// Los datos del acta que en el formulario del equipo resolvía MobiControl
/// (<c>%CustomAttr:Cedula%</c>, <c>%MODEL%</c>, …). Por enlace los manda el sistema de origen y
/// el asociado puede corregirlos antes de firmar.
/// </summary>
public class DatosActaEditables
{
    [MaxLength(20)]  public string? Cedula { get; set; }

    /// <summary>Nombre del tenedor, el que en el equipo venía del atributo <c>Usuario</c>.</summary>
    [MaxLength(200)] public string? Usuario { get; set; }

    /// <summary>A esta dirección van el enlace y la copia del acta.</summary>
    [MaxLength(200)] public string? Correo { get; set; }

    [MaxLength(100)] public string? Fabricante { get; set; }
    [MaxLength(100)] public string? Modelo { get; set; }
    [MaxLength(50)]  public string? Imei { get; set; }
    [MaxLength(50)]  public string? Iccid { get; set; }
    [MaxLength(30)]  public string? NumeroCelular { get; set; }
    [MaxLength(50)]  public string? Estado { get; set; }
    [MaxLength(100)] public string? Canal { get; set; }
    [MaxLength(100)] public string? Distrito { get; set; }

    /// <summary>Costo tal como se muestra en el acta ("$ 1.200.000", "1200000"…).</summary>
    [MaxLength(50)]  public string? Costo { get; set; }

    public string? Entregables { get; set; }

    [MaxLength(100)] public string? CiudadFirma { get; set; }
}

/// <summary>
/// Lo que manda el sistema de origen para pedir una firma. El identificador del equipo no es
/// editable después: es la identidad del dispositivo en MobiControl, y cambiarlo haría que el
/// acta marcara como entregado a otro equipo.
/// </summary>
public class CrearSolicitudRequest : DatosActaEditables
{
    /// <summary>Identificador de la solicitud en el sistema de origen.</summary>
    [Required, MaxLength(100)]
    public string IdSolicitud { get; set; } = string.Empty;

    /// <summary>Equivale a <c>%deviceid%</c>.</summary>
    [Required, MaxLength(100)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Días que el enlace acepta firmas. Por defecto, siete.</summary>
    [Range(1, 90)]
    public int? VigenciaDias { get; set; }
}

/// <summary>Lo que devuelve el formulario al firmar: los datos ya revisados y la firma.</summary>
public class FirmarSolicitudRequest : DatosActaEditables
{
    /// <summary>Nombre que el asociado confirmó o corrigió antes de firmar.</summary>
    [Required, MaxLength(200)]
    public string NombreAsociado { get; set; } = string.Empty;

    /// <summary>Firma en PNG: data URL (<c>data:image/png;base64,…</c>) o base64 puro.</summary>
    [Required]
    public string FirmaBase64 { get; set; } = string.Empty;
}

/// <summary>Datos precargados tal como quedaron guardados, ya normalizados.</summary>
public record DatosSolicitud(
    string DeviceId,
    string Cedula,
    string? Usuario,
    string? Correo,
    string? Fabricante,
    string? Modelo,
    string? Imei,
    string? Iccid,
    string? NumeroCelular,
    string? Estado,
    string? Canal,
    string? Distrito,
    string? Costo,
    string? Entregables,
    string? CiudadFirma);

/// <param name="Duplicada">True cuando el origen ya había pedido esta misma solicitud.</param>
/// <param name="UrlFirma">Enlace para firmar. Cada respuesta trae uno nuevo y todos siguen sirviendo.</param>
public record SolicitudCreadaResponse(
    Guid SolicitudUid,
    string IdSolicitud,
    string Estado,
    string UrlFirma,
    DateTime FechaVencimiento,
    bool Duplicada);

/// <summary>Estado de una solicitud, para que el origen pueda consultarla sin esperar el callback.</summary>
public record SolicitudDto(
    Guid SolicitudUid,
    string IdSolicitud,
    string Estado,
    DateTime FechaCreacion,
    DateTime FechaVencimiento,
    DateTime? FechaFirma,
    Guid? EntregaUid,
    string? EstadoCallback,
    int IntentosCallback,
    int? CodigoHttpCallback,
    string? UltimoErrorCallback,
    DateTime? FechaCallback);

/// <summary>Lo que el formulario web necesita para pintarse.</summary>
public record FormularioFirmaDto(
    string Estado,
    string Empresa,
    DateTime FechaVencimiento,
    DatosSolicitud Datos,
    Guid? EntregaUid,
    DateTime? FechaFirma,
    string? NombreAsociadoFirmante);

/// <summary>Respuesta al firmar: lo mínimo para mostrar la pantalla final.</summary>
public record FirmaRegistradaResponse(Guid EntregaUid, DateTime FechaFirma, string EstadoProceso, bool Duplicada);
