using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using MobiControlFirma.Application.Common.Interfaces;

namespace MobiControlFirma.Infrastructure.Storage;

/// <summary>
/// Guarda firmas y actas en Azure Blob Storage, que es lo que contempla el esquema original.
/// Se activa solo cuando hay cadena de conexión configurada.
/// </summary>
public class AlmacenamientoAzureBlob : IAlmacenamientoArchivos
{
    private readonly BlobServiceClient _cliente;

    public AlmacenamientoAzureBlob(IOptions<AlmacenamientoOptions> opciones)
    {
        var cadena = opciones.Value.AzureBlobConnectionString;
        if (string.IsNullOrWhiteSpace(cadena))
            throw new InvalidOperationException("Falta 'Almacenamiento:AzureBlobConnectionString'.");

        _cliente = new BlobServiceClient(cadena);
    }

    public async Task<ArchivoGuardado> GuardarAsync(
        string contenedor, string ruta, byte[] contenido, string tipoContenido, CancellationToken ct = default)
    {
        var contenedorCliente = _cliente.GetBlobContainerClient(contenedor);

        // Sin acceso público: las actas llevan cédula y firma, y se sirven a través del API.
        await contenedorCliente.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blob = contenedorCliente.GetBlobClient(ruta);
        using var flujo = new MemoryStream(contenido, writable: false);

        await blob.UploadAsync(flujo, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = tipoContenido },
        }, ct);

        return new ArchivoGuardado(
            contenedor, ruta, blob.Uri.ToString(), contenido.Length, SHA256.HashData(contenido));
    }

    public async Task<byte[]?> LeerAsync(string contenedor, string ruta, CancellationToken ct = default)
    {
        var blob = _cliente.GetBlobContainerClient(contenedor).GetBlobClient(ruta);

        try
        {
            var respuesta = await blob.DownloadContentAsync(ct);
            return respuesta.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
