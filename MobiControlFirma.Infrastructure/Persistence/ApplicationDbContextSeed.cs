using Microsoft.EntityFrameworkCore;
using MobiControlFirma.Domain.Entities;
using MobiControlFirma.Domain.Enums;

namespace MobiControlFirma.Infrastructure.Persistence;

/// <summary>
/// Datos mínimos para que la base sirva recién creada. Distritos y canales no se siembran a
/// propósito: llegan con cada acta desde los atributos personalizados de MobiControl y el API
/// los da de alta solo cuando aparecen, así nadie tiene que mantener una lista a mano.
/// </summary>
public static class ApplicationDbContextSeed
{
    private static readonly string[] EstadosBase = ["Nuevo", "Usado", "Reacondicionado", "Dañado"];

    public static async Task SeedAsync(ApplicationDbContext db, string urlBaseMobiControl, CancellationToken ct = default)
    {
        var existentes = await db.EstadosDispositivo.Select(e => e.Nombre).ToListAsync(ct);
        var faltantes = EstadosBase
            .Where(n => !existentes.Contains(n, StringComparer.OrdinalIgnoreCase))
            .Select(n => new EstadoDispositivo { Nombre = n });

        db.EstadosDispositivo.AddRange(faltantes);

        // Fila de configuración de MobiControl SIN credenciales. Deja constancia de a qué
        // consola apunta el ambiente; el usuario, la clave y el secreto viven en la
        // configuración de la aplicación (o en Key Vault), nunca en una fila consultable.
        var hayConfig = await db.ConfiguracionesIntegracion
            .AnyAsync(c => c.Proveedor == ProveedorIntegracion.MOBICONTROL, ct);

        if (!hayConfig && !string.IsNullOrWhiteSpace(urlBaseMobiControl))
        {
            db.ConfiguracionesIntegracion.Add(new IntegracionConfiguracion
            {
                Proveedor = ProveedorIntegracion.MOBICONTROL,
                Entorno = "PRODUCCION",
                TipoAutenticacion = TipoAutenticacion.OAUTH_PASSWORD,
                UrlBase = urlBaseMobiControl,
                ParametrosAdicionales =
                    """{"origenCredenciales":"appsettings:MobiControl","atributoFirma":"Firma de entrega","atributoFecha":"Fecha de entrega"}""",
                FechaCreacion = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
