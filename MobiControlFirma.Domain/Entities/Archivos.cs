namespace MobiControlFirma.Domain.Entities;

/// <summary>
/// Referencia a la imagen de la firma. El binario vive en el almacenamiento de archivos
/// (Azure Blob en producción, disco local en desarrollo); aquí solo queda la ruta y el
/// hash para poder demostrar que el archivo no se alteró después.
/// </summary>
public class Firma
{
    public int FirmaId { get; set; }
    public int EntregaId { get; set; }
    public EntregaDispositivo Entrega { get; set; } = null!;

    public string NombreContenedor { get; set; } = "firmas";
    public string RutaBlob { get; set; } = string.Empty;
    public string? UrlBlob { get; set; }
    public string FormatoImagen { get; set; } = "image/png";
    public int? TamanoBytes { get; set; }
    public byte[]? HashSHA256 { get; set; }
    public DateTime FechaCaptura { get; set; }
}

/// <summary>Referencia al acta en PDF generada por el API (misma lógica que <see cref="Firma"/>).</summary>
public class DocumentoPdf
{
    public int DocumentoId { get; set; }
    public int EntregaId { get; set; }
    public EntregaDispositivo Entrega { get; set; } = null!;

    public string NombreContenedor { get; set; } = "documentos-pdf";
    public string RutaBlob { get; set; } = string.Empty;
    public string? UrlBlob { get; set; }
    public string NombreArchivo { get; set; } = "firmaentregadispositivo.pdf";
    public int? TamanoBytes { get; set; }
    public byte[]? HashSHA256 { get; set; }
    public DateTime FechaGeneracion { get; set; }
}
