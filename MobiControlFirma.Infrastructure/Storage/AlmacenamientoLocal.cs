using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MobiControlFirma.Application.Common.Interfaces;

namespace MobiControlFirma.Infrastructure.Storage;

/// <summary>Opciones del almacenamiento de firmas y actas.</summary>
public class AlmacenamientoOptions
{
    public const string SectionName = "Almacenamiento";

    /// <summary>Cadena de Azure Blob. Vacía: se guarda en disco (desarrollo y servidor propio).</summary>
    public string? AzureBlobConnectionString { get; set; }

    /// <summary>Carpeta raíz del almacenamiento en disco, relativa al API si no es absoluta.</summary>
    public string RutaLocal { get; set; } = "almacenamiento";

    public string ContenedorFirmas { get; set; } = "firmas";
    public string ContenedorDocumentos { get; set; } = "documentos-pdf";
}

/// <summary>
/// Guarda los archivos en el disco del servidor. Es la implementación por defecto: el API vive
/// hoy en un servidor propio y no hay una cuenta de Azure de por medio.
/// </summary>
public class AlmacenamientoLocal(IOptions<AlmacenamientoOptions> opciones, IHostEnvironment entorno)
    : IAlmacenamientoArchivos
{
    private readonly AlmacenamientoOptions _opciones = opciones.Value;

    private string Raiz => Path.IsPathRooted(_opciones.RutaLocal)
        ? _opciones.RutaLocal
        : Path.Combine(entorno.ContentRootPath, _opciones.RutaLocal);

    public async Task<ArchivoGuardado> GuardarAsync(
        string contenedor, string ruta, byte[] contenido, string tipoContenido, CancellationToken ct = default)
    {
        var destino = ResolverRuta(contenedor, ruta);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await File.WriteAllBytesAsync(destino, contenido, ct);

        return new ArchivoGuardado(contenedor, ruta, null, contenido.Length, SHA256.HashData(contenido));
    }

    public async Task<byte[]?> LeerAsync(string contenedor, string ruta, CancellationToken ct = default)
    {
        var origen = ResolverRuta(contenedor, ruta);
        return File.Exists(origen) ? await File.ReadAllBytesAsync(origen, ct) : null;
    }

    /// <summary>
    /// Compone la ruta física y verifica que no se salga de la raíz: la ruta se arma con datos
    /// guardados en la base, y un "../" ahí adentro leería archivos del servidor.
    /// </summary>
    private string ResolverRuta(string contenedor, string ruta)
    {
        var raiz = Path.GetFullPath(Raiz);
        var completa = Path.GetFullPath(Path.Combine(raiz, contenedor, ruta.Replace('/', Path.DirectorySeparatorChar)));

        if (!completa.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Ruta de archivo fuera del almacenamiento: {ruta}");

        return completa;
    }
}
