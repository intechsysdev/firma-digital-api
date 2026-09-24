using MobiControlFirma.Domain.Enums;

namespace MobiControlFirma.Domain.Entities;

/// <summary>
/// Pedido de firma que llega de un sistema de origen. Reemplaza, para la firma por enlace, lo
/// que MobiControl hacía al instalar el formulario: en vez de resolver variables en el equipo,
/// el origen manda los mismos datos y este sistema arma un enlace con ellos precargados.
///
/// Los datos se guardan tal como llegaron y no se tocan después. El acta que resulte de firmar
/// guarda lo que el asociado confirmó; entre las dos queda a la vista cualquier corrección que
/// haya hecho antes de firmar.
/// </summary>
public class SolicitudFirma : IDeEmpresa
{
    public int SolicitudId { get; set; }

    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>Identificador público. Es el que va cifrado dentro del enlace de firma.</summary>
    public Guid SolicitudUid { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Identificador que manda el sistema de origen. Único por empresa: un reintento del origen
    /// con el mismo identificador devuelve la solicitud que ya existe en vez de crear otra.
    /// </summary>
    public string IdSolicitudOrigen { get; set; } = string.Empty;

    /// <summary>Datos precargados en JSON, tal como los envió el origen ya normalizados.</summary>
    public string DatosOrigen { get; set; } = "{}";

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.PENDIENTE;

    public DateTime FechaCreacion { get; set; }

    /// <summary>Pasada esta fecha el enlace deja de aceptar firmas.</summary>
    public DateTime FechaVencimiento { get; set; }

    public DateTime? FechaFirma { get; set; }

    /// <summary>Acta que resultó de firmar. Null mientras la solicitud está pendiente.</summary>
    public int? EntregaId { get; set; }
    public EntregaDispositivo? Entrega { get; set; }

    // ---- Aviso al sistema de origen ----
    // Una sola notificación por solicitud, así que la bandeja vive en la misma fila en vez de
    // en una tabla aparte. Sale en segundo plano: el asociado no espera a que el origen conteste.

    /// <summary>Null hasta que se firma: antes no hay nada que avisar.</summary>
    public EstadoCallback? EstadoCallback { get; set; }

    public int IntentosCallback { get; set; }
    public int? CodigoHttpCallback { get; set; }
    public string? UltimoErrorCallback { get; set; }

    /// <summary>Cuándo volver a intentarlo. Se espacia tras cada fallo.</summary>
    public DateTime? ProximoIntentoCallback { get; set; }

    public DateTime? FechaCallback { get; set; }
}
