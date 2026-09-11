using Microsoft.EntityFrameworkCore;
using MobiControlFirma.Domain.Entities;

namespace MobiControlFirma.Infrastructure.Persistence;

/// <summary>
/// Datos mínimos para que una empresa sirva recién creada. Distritos y canales no se siembran a
/// propósito: llegan con cada acta desde los atributos personalizados de MobiControl y el API
/// los da de alta solo cuando aparecen, así nadie tiene que mantener una lista a mano.
/// </summary>
public static class ApplicationDbContextSeed
{
    private static readonly string[] EstadosBase = ["Nuevo", "Usado", "Reacondicionado", "Dañado"];

    /// <summary>
    /// Siembra los estados base de una empresa. Se llama al crearla y también al arrancar, para
    /// que una empresa dada de alta antes de que existiera un estado nuevo también lo reciba.
    /// </summary>
    public static async Task SembrarEmpresaAsync(ApplicationDbContext db, int empresaId, CancellationToken ct = default)
    {
        // IgnoreQueryFilters porque esto corre fuera de una petición: no hay empresa en contexto
        // y el filtro global dejaría la consulta vacía, sembrando duplicados en cada arranque.
        var existentes = await db.EstadosDispositivo
            .IgnoreQueryFilters()
            .Where(e => e.EmpresaId == empresaId)
            .Select(e => e.Nombre)
            .ToListAsync(ct);

        var faltantes = EstadosBase
            .Where(n => !existentes.Contains(n, StringComparer.OrdinalIgnoreCase))
            .Select(n => new EstadoDispositivo { EmpresaId = empresaId, Nombre = n })
            .ToList();

        if (faltantes.Count == 0) return;

        db.EstadosDispositivo.AddRange(faltantes);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Siembra los estados base de todas las empresas activas.</summary>
    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var empresas = await db.Empresas.Where(e => e.Activo).Select(e => e.EmpresaId).ToListAsync(ct);

        foreach (var empresaId in empresas)
            await SembrarEmpresaAsync(db, empresaId, ct);
    }
}
