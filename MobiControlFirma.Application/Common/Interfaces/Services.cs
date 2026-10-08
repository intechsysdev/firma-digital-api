using MobiControlFirma.Application.Entregas;

namespace MobiControlFirma.Application.Common.Interfaces;

/// <summary>Archivo guardado: dónde quedó y con qué integridad.</summary>
/// <param name="Contenedor">Contenedor de Azure Blob, o carpeta raíz en disco.</param>
/// <param name="Ruta">Ruta relativa dentro del contenedor.</param>
/// <param name="Url">URL absoluta cuando el almacenamiento la expone; nula en disco local.</param>
/// <param name="TamanoBytes">Tamaño real de lo escrito.</param>
/// <param name="HashSha256">Huella del contenido, para verificar que nadie lo cambió después.</param>
public record ArchivoGuardado(string Contenedor, string Ruta, string? Url, int TamanoBytes, byte[] HashSha256);

/// <summary>Guarda y recupera firmas y actas. Se implementa contra disco local o Azure Blob.</summary>
public interface IAlmacenamientoArchivos
{
    Task<ArchivoGuardado> GuardarAsync(
        string contenedor, string ruta, byte[] contenido, string tipoContenido, CancellationToken ct = default);

    /// <summary>Devuelve el contenido, o null si el archivo ya no está.</summary>
    Task<byte[]?> LeerAsync(string contenedor, string ruta, CancellationToken ct = default);
}

/// <summary>Arma el acta de entrega en PDF a partir de los datos firmados.</summary>
public interface IGeneradorActaPdf
{
    byte[] Generar(DatosActa datos, byte[] firmaPng);
}

/// <summary>Resultado de una llamada a MobiControl, listo para dejar en la bitácora.</summary>
/// <param name="Accion">Ver <c>TipoAccionIntegracion</c>.</param>
public record ResultadoIntegracion(string Accion, bool Exitoso, int? CodigoHttp, string? MensajeError);

/// <summary>Cliente de la API de MobiControl (token, atributos personalizados y check-in).</summary>
public interface IClienteMobiControl
{
    /// <summary>
    /// False cuando la empresa de la petición no tiene consola configurada: el API sigue
    /// firmando actas, solo que quedan sin sincronizar. Es asíncrono porque la configuración
    /// vive en la base, una por empresa, y no en un archivo del servidor.
    /// </summary>
    Task<bool> EstaConfiguradoAsync(CancellationToken ct = default);

    /// <summary>
    /// Marca el equipo como firmado y le pide un check-in inmediato. Devuelve una entrada de
    /// bitácora por cada llamada realizada (token, atributos, check-in).
    /// </summary>
    Task<IReadOnlyList<ResultadoIntegracion>> MarcarEntregaFirmadaAsync(
        string deviceId, DateOnly fechaEntrega, CancellationToken ct = default);

    // ---- Geolocalización ----
    // Todas lanzan ErrorSolicitudException con un mensaje para el usuario cuando la empresa no
    // tiene consola configurada o MobiControl no responde: quien consulta el mapa necesita saber
    // por qué no ve nada, no una lista vacía.

    /// <summary>Todos los equipos de la consola de la empresa, con su estado.</summary>
    Task<IReadOnlyList<EquipoMobiControl>> ListarEquiposAsync(CancellationToken ct = default);

    /// <summary>Última posición conocida, o null si el equipo nunca reportó una.</summary>
    Task<UbicacionMobiControl?> UltimaUbicacionAsync(string deviceId, CancellationToken ct = default);

    /// <summary>Puntos GPS recolectados entre dos momentos, en orden cronológico.</summary>
    Task<IReadOnlyList<UbicacionMobiControl>> RecorridoAsync(
        string deviceId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct = default);

    /// <summary>Le pide al equipo que reporte su posición ahora. La respuesta llega después, no aquí.</summary>
    Task LocalizarAsync(string deviceId, CancellationToken ct = default);

    // ---- Atributos personalizados ----
    // Las definiciones son de toda la consola; los valores, de cada equipo. Lanzan
    // ErrorSolicitudException con el motivo cuando MobiControl rechaza la operación.

    Task<IReadOnlyList<AtributoMobiControl>> ListarAtributosAsync(CancellationToken ct = default);

    Task<AtributoMobiControl> CrearAtributoAsync(AtributoMobiControl atributo, CancellationToken ct = default);

    /// <summary>Cambia nombre, tipo, opciones y si se pasa al equipo. Conserva el ReferenceId.</summary>
    Task<AtributoMobiControl> ActualizarAtributoAsync(string nombreActual, AtributoMobiControl atributo, CancellationToken ct = default);

    /// <summary>False si MobiControl ya no lo tenía.</summary>
    Task<bool> EliminarAtributoAsync(string nombre, CancellationToken ct = default);

    /// <summary>Los valores de todos los atributos en un equipo, también los heredados de su grupo.</summary>
    Task<IReadOnlyList<ValorAtributoMobiControl>> ValoresAtributosAsync(string deviceId, CancellationToken ct = default);

    /// <summary>Valor con su tipo JSON: bool para sí/no, número para los numéricos, texto para lo demás.</summary>
    Task GuardarValoresAtributosAsync(string deviceId, IReadOnlyList<(string Nombre, object Valor)> valores, CancellationToken ct = default);
}

/// <summary>Tipos de atributo tal como los nombra MobiControl.</summary>
public static class TiposAtributo
{
    public const string Texto = "Text";
    public const string Lista = "Enumerator";
    public const string Fecha = "Date";
    public const string Numero = "Float";
    public const string SiNo = "Boolean";

    public static readonly IReadOnlyList<string> Todos = [Texto, Lista, Fecha, Numero, SiNo];
}

/// <param name="Tipo">Ver <see cref="TiposAtributo"/>.</param>
/// <param name="Opciones">Solo para las listas (Enumerator).</param>
/// <param name="PasaAlEquipo">PropagateToDevice: el valor se copia al equipo y sus apps lo pueden leer.</param>
public record AtributoMobiControl(string Nombre, string Tipo, IReadOnlyList<string> Opciones, bool PasaAlEquipo, string? ReferenceId = null);

/// <param name="Heredado">El equipo no tiene valor propio: lo toma de su grupo.</param>
/// <param name="Origen">Grupo del que lo hereda, si MobiControl lo dice.</param>
public record ValorAtributoMobiControl(string Nombre, string? Valor, bool Heredado, string? Origen = null);

/// <summary>Un equipo tal como lo ve MobiControl, reducido a lo que usa Geolocalización.</summary>
public record EquipoMobiControl(
    string DeviceId,
    string Nombre,
    string? Plataforma,
    string? Fabricante,
    string? Modelo,
    bool EnLinea,
    int? Bateria,
    bool? Cargando,
    DateTimeOffset? UltimoReporte,
    string? Grupo,
    string? Imei,
    string? Telefono,
    string? Serial)
{
    /// <summary>Solo teléfonos y tabletas reportan posición: pedirla a un Mac o a un PC es una llamada perdida.</summary>
    public bool TieneGps => Plataforma is "Android" or "iOS";
}

public record UbicacionMobiControl(
    double Latitud, double Longitud, DateTimeOffset Momento, double? Velocidad, double? Rumbo);

/// <summary>Resultado de un intento de envío, para dejarlo registrado en la bandeja.</summary>
public record ResultadoCorreo(bool Exitoso, int? CodigoHttp, string? Detalle);

/// <summary>Manda la copia del acta a sus destinatarios.</summary>
public interface IEnviadorCorreo
{
    Task<ResultadoCorreo> EnviarActaAsync(
        ConfiguracionEmpresa config,
        IReadOnlyList<string> destinatarios,
        string asunto,
        string cuerpoHtml,
        string nombreArchivo,
        byte[] pdf,
        CancellationToken ct = default);
}
