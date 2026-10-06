using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MobiControlFirma.Application.Common;
using MobiControlFirma.Domain.Entities;

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

    /// <summary>Celular, Tableta, PC… Texto libre.</summary>
    [MaxLength(30)]  public string? TipoDispositivo { get; set; }

    /// <summary>Serial del fabricante. Identifica equipos sin IMEI.</summary>
    [MaxLength(100)] public string? Serial { get; set; }

    [MaxLength(100)] public string? Fabricante { get; set; }
    [MaxLength(100)] public string? Modelo { get; set; }
    [MaxLength(50)]  public string? Imei { get; set; }
    [MaxLength(50)]  public string? Iccid { get; set; }
    [MaxLength(30)]  public string? NumeroCelular { get; set; }
    [MaxLength(50)]  public string? Estado { get; set; }
    [MaxLength(100)] public string? Canal { get; set; }
    [MaxLength(100)] public string? Distrito { get; set; }

    /// <summary>Costo tal como se muestra en el acta ("$ 1.200.000", "1200000"…). Acepta número.</summary>
    [MaxLength(50)]
    [JsonConverter(typeof(TextoONumeroJsonConverter))]
    public string? Costo { get; set; }

    public string? Entregables { get; set; }

    [MaxLength(100)] public string? CiudadFirma { get; set; }
}

/// <summary>
/// Lo que manda el sistema de origen para pedir una firma. El identificador del equipo en
/// MobiControl no es editable después: es con el que se marca la entrega, y cambiarlo haría que el
/// acta marcara como entregado a otro equipo.
/// </summary>
public class CrearSolicitudRequest : DatosActaEditables
{
    /// <summary>
    /// Identificador de la solicitud en el sistema de origen (en HV, el ID de SharePoint). Es la
    /// llave de la integración: vuelve tal cual en la consulta y en el callback.
    /// </summary>
    [MaxLength(100)]
    [JsonConverter(typeof(TextoONumeroJsonConverter))]
    public string IdSolicitud { get; set; } = string.Empty;

    // ---- Nombres del contrato de SharePoint ----
    // HV manda los campos con sus propios nombres. Son alias de escritura de los de arriba: llegan
    // a los mismos datos, y los nombres anteriores (idSolicitud, cedula, fabricante…) siguen
    // sirviendo. Los que coinciden sin importar mayúsculas (IMEI, Serial, Canal…) no necesitan alias.

    /// <summary>Alias de <see cref="IdSolicitud"/>: el ID del elemento de SharePoint, texto o número.</summary>
    [JsonPropertyName("SharePointId")]
    [JsonConverter(typeof(TextoONumeroJsonConverter))]
    public string? SharePointId { set { if (!string.IsNullOrWhiteSpace(value)) IdSolicitud = value; } }

    /// <summary>Alias de <see cref="DatosActaEditables.Cedula"/>.</summary>
    [JsonPropertyName("NumeroCedula")]
    [JsonConverter(typeof(TextoONumeroJsonConverter))]
    public string? NumeroCedula { set { if (value is not null) Cedula = value; } }

    /// <summary>Alias de <see cref="DatosActaEditables.Usuario"/>: el responsable del equipo.</summary>
    [JsonPropertyName("NombreAsociado")]
    public string? NombreAsociado { set { if (value is not null) Usuario = value; } }

    /// <summary>Alias de <see cref="DatosActaEditables.Fabricante"/>.</summary>
    [JsonPropertyName("Marca")]
    public string? Marca { set { if (value is not null) Fabricante = value; } }

    /// <summary>Alias de <see cref="DatosActaEditables.Iccid"/>.</summary>
    [JsonPropertyName("SIMCard")]
    [JsonConverter(typeof(TextoONumeroJsonConverter))]
    public string? SimCard { set { if (value is not null) Iccid = value; } }

    /// <summary>Alias de <see cref="DatosActaEditables.Costo"/>; puede llegar como número.</summary>
    [JsonPropertyName("CostoEquipo")]
    [JsonConverter(typeof(TextoONumeroJsonConverter))]
    public string? CostoEquipo { set { if (value is not null) Costo = value; } }

    /// <summary>Alias de <see cref="NombreDeInterfaz"/>.</summary>
    [JsonPropertyName("NombreInterfaz")]
    public string? NombreInterfaz { set { if (value is not null) NombreDeInterfaz = value; } }

    /// <summary>
    /// Nombre de la empresa en el origen. Solo informativo: la empresa la define la credencial con
    /// la que se llama, no lo que diga el cuerpo.
    /// </summary>
    [MaxLength(200)]
    public string? Empresa { get; set; }

    /// <summary>
    /// Fecha de entrega del equipo. Se imprime en el acta y es la que se escribe en MobiControl.
    /// Si no llega, se usa el día siguiente a la firma.
    /// </summary>
    public DateOnly? FechaEntrega { get; set; }

    /// <summary>
    /// Equivale a <c>%deviceid%</c>. Opcional: si no llega, se busca el equipo en MobiControl por
    /// IMEI o serial al firmar; si no está en la consola, el acta se firma sin marcarlo.
    /// </summary>
    [MaxLength(100)]
    public string? DeviceId { get; set; }

    /// <summary>Días que el enlace acepta firmas. Por defecto, siete.</summary>
    [Range(1, 90)]
    public int? VigenciaDias { get; set; }

    /// <summary>
    /// SI: el equipo está en MobiControl. Se valida al recibir la solicitud que la consola lo
    /// tenga (por IMEI, serial o deviceId) y la entrega se marca allí al firmar. NO: no se toca
    /// MobiControl; se firma por enlace. Sin valor, se busca en MobiControl al firmar si se puede.
    /// </summary>
    [JsonConverter(typeof(SiNoJsonConverter))]
    public bool? DispositivoConMobicontrol { get; set; }

    /// <summary>
    /// SI: deviceId, nombre, marca y modelo se toman de MobiControl en vez de lo enviado (lo
    /// enviado queda para lo que la consola no tenga). Implica que el equipo está en MobiControl.
    /// </summary>
    [JsonConverter(typeof(SiNoJsonConverter))]
    public bool? DatosDesdeMobicontrol { get; set; }

    /// <summary>Sistema que envía la solicitud ("SharePoint"). Vuelve en la consulta.</summary>
    [MaxLength(50)]
    public string? NombreDeInterfaz { get; set; }
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
    string? DeviceId,
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
    string? CiudadFirma,
    string? TipoDispositivo = null,
    string? Serial = null,
    DateOnly? FechaEntrega = null,
    // Nombre del equipo en MobiControl, cuando los datos se tomaron de allí.
    string? NombreDispositivo = null,
    [property: JsonConverter(typeof(SiNoJsonConverter))] bool? DispositivoConMobicontrol = null,
    [property: JsonConverter(typeof(SiNoJsonConverter))] bool? DatosDesdeMobicontrol = null,
    string? NombreDeInterfaz = null);

/// <param name="Duplicada">True cuando el origen ya había pedido esta misma solicitud.</param>
/// <param name="UrlFirma">Enlace para firmar. Cada respuesta trae uno nuevo y todos siguen sirviendo.</param>
/// <param name="UrlDocumento">PDF del acta, si ya se firmó.</param>
public record SolicitudCreadaResponse(
    Guid SolicitudUid,
    string IdSolicitud,
    string Estado,
    string UrlFirma,
    DateTime FechaVencimiento,
    bool Duplicada,
    DateTime? FechaFirma = null,
    string? UrlDocumento = null);

/// <summary>
/// Respuesta de <c>POST /solicitudes</c> con el contrato de SharePoint. <c>UrlDocumento</c> es el
/// enlace para firmar mientras la solicitud está pendiente, y el PDF del acta una vez firmada.
/// </summary>
public record RespuestaSolicitudSharePoint(
    [property: JsonPropertyName("SharePointId")] string SharePointId,
    [property: JsonPropertyName("Estado")] string Estado,
    [property: JsonPropertyName("FechaFirma")] DateTime? FechaFirma,
    [property: JsonPropertyName("UrlDocumento")] string? UrlDocumento)
{
    public static RespuestaSolicitudSharePoint Desde(SolicitudCreadaResponse s) => s.Estado switch
    {
        "FIRMADA" => new(s.IdSolicitud, "Firmado", s.FechaFirma, s.UrlDocumento),
        "RECHAZADA" => new(s.IdSolicitud, "Rechazado", null, null),
        "VENCIDA" => new(s.IdSolicitud, "Vencido", null, null),
        _ => new(s.IdSolicitud, "Pendiente", null, s.UrlFirma),
    };
}

/// <summary>
/// Todo lo de una solicitud: lo que mandó el origen, en qué va y, si se firmó, el acta tal como
/// quedó. Es lo que el origen consulta cuando no recibe avisos.
/// </summary>
/// <param name="Datos">Lo que mandó el origen, ya normalizado.</param>
/// <param name="Acta">Lo que se firmó, con las correcciones que se hayan hecho al firmar. Null si no se ha firmado.</param>
/// <param name="UrlDocumento">Descarga del PDF sin credenciales. Null si no se ha firmado.</param>
public record SolicitudDto(
    string IdSolicitud,
    Guid SolicitudUid,
    string Estado,
    DateTime FechaCreacion,
    DateTime FechaActualizacion,
    DateTime FechaVencimiento,
    DateTime? FechaFirma,
    DateTime? FechaRechazo,
    string? MotivoRechazo,
    string? RechazadoPor,
    string? UrlDocumento,
    DatosSolicitud Datos,
    ActaFirmadaDto? Acta,
    Guid? EntregaUid,
    string? EstadoCallback,
    int IntentosCallback,
    int? CodigoHttpCallback,
    string? UltimoErrorCallback,
    DateTime? FechaCallback);

/// <summary>Fila del listado de solicitudes: lo justo para saber cuáles consultar.</summary>
public record SolicitudResumenDto(
    string IdSolicitud,
    string Estado,
    DateTime FechaCreacion,
    DateTime FechaActualizacion,
    DateTime? FechaFirma,
    DateTime? FechaRechazo);

/// <summary>
/// Lo que el formulario del equipo recibe al buscar su solicitud por IMEI o serial: los datos que
/// MobiControl no tiene y el identificador con el que el acta queda atada a la solicitud.
/// </summary>
public record PrecargaEquipoDto(
    string IdSolicitud,
    Guid SolicitudUid,
    DateTime FechaVencimiento,
    DatosSolicitud Datos);

/// <summary>El acta que resultó de una solicitud, con los datos que se firmaron.</summary>
public record ActaFirmadaDto(
    Guid EntregaUid,
    string Numero,
    DateTime FechaFirma,
    string? CiudadFirma,
    DateOnly? FechaEntrega,
    string EstadoProceso,
    FirmanteActaDto Firmante,
    EquipoActaDto Equipo)
{
    /// <summary>Requiere la entrega con su empleado, dispositivo, estado, canal y distrito.</summary>
    public static ActaFirmadaDto Desde(EntregaDispositivo entrega) => new(
        entrega.EntregaUid,
        entrega.EntregaUid.ToString()[..8].ToUpperInvariant(),
        // La base guarda UTC pero lo devuelve sin marcar; sin la Z se leería como hora local.
        DateTime.SpecifyKind(entrega.FechaFirma, DateTimeKind.Utc),
        entrega.CiudadFirma,
        entrega.FechaEntregaProgramada,
        entrega.EstadoProceso.ToString(),
        new FirmanteActaDto(
            entrega.NombreAsociadoFirmante,
            entrega.Empleado.NombreCompleto,
            entrega.Empleado.Cedula,
            entrega.CorreoAsociado),
        new EquipoActaDto(
            entrega.Dispositivo.MobiControlDeviceId,
            entrega.Dispositivo.TipoDispositivo,
            entrega.Dispositivo.Serial,
            entrega.Dispositivo.Fabricante,
            entrega.Dispositivo.Modelo,
            entrega.Dispositivo.IMEI,
            entrega.ICCID,
            entrega.NumeroCelular,
            entrega.Estado?.Nombre,
            entrega.Canal?.Nombre,
            entrega.Distrito?.Nombre,
            entrega.CostoEquipo,
            entrega.Entregables));
}

/// <param name="Nombre">Quien firmó, tal como lo confirmó.</param>
/// <param name="NombreTenedor">El responsable registrado del equipo.</param>
public record FirmanteActaDto(string Nombre, string NombreTenedor, string Cedula, string? Correo);

public record EquipoActaDto(
    string? DeviceId,
    string? TipoDispositivo,
    string? Serial,
    string? Fabricante,
    string? Modelo,
    string? Imei,
    string? Iccid,
    string? NumeroCelular,
    string? Estado,
    string? Canal,
    string? Distrito,
    decimal? Costo,
    string? Entregables);

/// <summary>Lo que el formulario web necesita para pintarse.</summary>
public record FormularioFirmaDto(
    string Estado,
    string Empresa,
    DateTime FechaVencimiento,
    DatosSolicitud Datos,
    Guid? EntregaUid,
    DateTime? FechaFirma,
    string? NombreAsociadoFirmante,
    DateTime? FechaRechazo = null,
    string? MotivoRechazo = null);

/// <summary>El asociado no acepta el acta.</summary>
public class RechazarSolicitudRequest
{
    /// <summary>Por qué no la acepta. Va al sistema de origen en el aviso.</summary>
    [Required, MaxLength(500)]
    public string Motivo { get; set; } = string.Empty;

    /// <summary>Nombre de quien rechaza, si lo escribe.</summary>
    [MaxLength(200)]
    public string? Nombre { get; set; }
}

public record RechazoRegistradoResponse(DateTime FechaRechazo, bool Duplicado);

/// <summary>Respuesta al firmar: lo mínimo para mostrar la pantalla final.</summary>
public record FirmaRegistradaResponse(Guid EntregaUid, DateTime FechaFirma, string EstadoProceso, bool Duplicada);
