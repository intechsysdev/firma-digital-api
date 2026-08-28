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
    /// <summary>False cuando faltan credenciales: el API sigue firmando, solo no sincroniza.</summary>
    bool EstaConfigurado { get; }

    /// <summary>
    /// Marca el equipo como firmado y le pide un check-in inmediato. Devuelve una entrada de
    /// bitácora por cada llamada realizada (token, atributos, check-in).
    /// </summary>
    Task<IReadOnlyList<ResultadoIntegracion>> MarcarEntregaFirmadaAsync(
        string deviceId, DateOnly fechaEntrega, CancellationToken ct = default);
}
