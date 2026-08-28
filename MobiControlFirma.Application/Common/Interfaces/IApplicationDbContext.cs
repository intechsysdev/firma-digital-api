using Microsoft.EntityFrameworkCore;
using MobiControlFirma.Domain.Entities;

namespace MobiControlFirma.Application.Common.Interfaces;

/// <summary>Abstracción del contexto de datos usada por la capa de aplicación.</summary>
public interface IApplicationDbContext
{
    DbSet<Distrito> Distritos { get; }
    DbSet<Canal> Canales { get; }
    DbSet<EstadoDispositivo> EstadosDispositivo { get; }
    DbSet<Empleado> Empleados { get; }
    DbSet<Dispositivo> Dispositivos { get; }
    DbSet<EntregaDispositivo> Entregas { get; }
    DbSet<Firma> Firmas { get; }
    DbSet<DocumentoPdf> DocumentosPdf { get; }
    DbSet<IntegracionSincronizacion> Sincronizaciones { get; }
    DbSet<IntegracionConfiguracion> ConfiguracionesIntegracion { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
