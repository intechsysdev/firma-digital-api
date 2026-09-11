using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Infrastructure.Identidad;
using MobiControlFirma.Domain.Entities;
using MobiControlFirma.Domain.Enums;

namespace MobiControlFirma.Infrastructure.Persistence;

/// <summary>
/// Contexto de <c>mobicontrol_firmas_db</c>. Los nombres de tablas y columnas se fijan a mano
/// para que coincidan con el esquema entregado por el cliente (db/schema.sql).
/// </summary>
public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IContextoEmpresa? contextoEmpresa = null)
    : IdentityDbContext<UsuarioAdmin>(options), IApplicationDbContext
{
    // Se leen en cada consulta, no al construir el contexto: la empresa se resuelve cuando ya
    // se autenticó la petición, que puede ser después de que el contexto exista.
    private int? EmpresaFiltro => contextoEmpresa?.EmpresaId;

    // Sin contexto de empresa —herramientas de EF, migraciones— no se filtra nada; con
    // superadministrador tampoco, porque su trabajo es ver todas las empresas.
    private bool SinFiltro => contextoEmpresa is null || contextoEmpresa.EsSuperAdministrador;

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Distrito> Distritos => Set<Distrito>();
    public DbSet<Canal> Canales => Set<Canal>();
    public DbSet<EstadoDispositivo> EstadosDispositivo => Set<EstadoDispositivo>();
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Dispositivo> Dispositivos => Set<Dispositivo>();
    public DbSet<EntregaDispositivo> Entregas => Set<EntregaDispositivo>();
    public DbSet<Firma> Firmas => Set<Firma>();
    public DbSet<DocumentoPdf> DocumentosPdf => Set<DocumentoPdf>();
    public DbSet<IntegracionSincronizacion> Sincronizaciones => Set<IntegracionSincronizacion>();
    public DbSet<IntegracionConfiguracion> ConfiguracionesIntegracion => Set<IntegracionConfiguracion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---------------- Catálogos ----------------
        builder.Entity<Distrito>(e =>
        {
            e.ToTable("Distritos");
            e.HasKey(x => x.DistritoId);
            e.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.EmpresaId, x.Nombre }).IsUnique();
            e.Property(x => x.Activo).HasDefaultValue(true);
        });

        builder.Entity<Canal>(e =>
        {
            e.ToTable("Canales");
            e.HasKey(x => x.CanalId);
            e.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.EmpresaId, x.Nombre }).IsUnique();
            e.Property(x => x.Activo).HasDefaultValue(true);
        });

        builder.Entity<EstadoDispositivo>(e =>
        {
            e.ToTable("EstadosDispositivo");
            e.HasKey(x => x.EstadoId);
            e.Property(x => x.Nombre).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.EmpresaId, x.Nombre }).IsUnique();
        });

        // ---------------- Empleados ----------------
        builder.Entity<Empleado>(e =>
        {
            e.ToTable("Empleados");
            e.HasKey(x => x.EmpleadoId);
            e.Property(x => x.Cedula).HasColumnType("varchar(20)").IsRequired();
            e.HasIndex(x => new { x.EmpresaId, x.Cedula }).IsUnique();
            e.Property(x => x.NombreCompleto).HasMaxLength(200).IsRequired();
            e.Property(x => x.Activo).HasDefaultValue(true);
            e.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");

            e.HasOne(x => x.Canal).WithMany().HasForeignKey(x => x.CanalId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Distrito).WithMany().HasForeignKey(x => x.DistritoId).OnDelete(DeleteBehavior.NoAction);
        });

        // ---------------- Dispositivos ----------------
        builder.Entity<Dispositivo>(e =>
        {
            e.ToTable("Dispositivos");
            e.HasKey(x => x.DispositivoId);
            e.Property(x => x.MobiControlDeviceId).HasColumnType("varchar(100)").IsRequired();
            e.HasIndex(x => new { x.EmpresaId, x.MobiControlDeviceId }).IsUnique();
            e.Property(x => x.Fabricante).HasMaxLength(100);
            e.Property(x => x.Modelo).HasMaxLength(100);
            e.Property(x => x.IMEI).HasColumnType("varchar(50)");
            e.Property(x => x.ICCID).HasColumnType("varchar(50)");
            e.Property(x => x.NumeroCelular).HasColumnType("varchar(30)");
            e.Property(x => x.CostoEquipo).HasPrecision(12, 2);
            e.Property(x => x.Activo).HasDefaultValue(true);
            e.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");

            // Índice filtrado y no un UNIQUE liso: SQL Server solo admite un NULL en una
            // restricción única, y en la flota hay equipos que todavía no reportan IMEI. Con el
            // UNIQUE del esquema original, el segundo equipo sin IMEI fallaba al insertar.
            e.HasIndex(x => new { x.EmpresaId, x.IMEI }).IsUnique().HasFilter("[IMEI] IS NOT NULL");

            e.HasOne(x => x.EstadoActual).WithMany()
                .HasForeignKey(x => x.EstadoActualId).OnDelete(DeleteBehavior.NoAction);
        });

        // ---------------- Actas de entrega ----------------
        builder.Entity<EntregaDispositivo>(e =>
        {
            e.ToTable("EntregasDispositivo", t => t.HasCheckConstraint(
                "CK_EntregasDispositivo_EstadoProceso",
                "[EstadoProceso] IN ('FIRMADO','SINCRONIZADO','ERROR_SINCRONIZACION')"));

            e.HasKey(x => x.EntregaId);
            e.Property(x => x.EntregaUid).HasDefaultValueSql("NEWID()");
            e.HasIndex(x => x.EntregaUid).IsUnique();

            e.Property(x => x.NombreAsociadoFirmante).HasMaxLength(200).IsRequired();
            e.Property(x => x.CiudadFirma).HasColumnType("varchar(100)").IsRequired().HasDefaultValue("Cali");
            e.Property(x => x.ICCID).HasColumnType("varchar(50)");
            e.Property(x => x.NumeroCelular).HasColumnType("varchar(30)");
            e.Property(x => x.CostoEquipo).HasPrecision(12, 2);
            e.Property(x => x.FechaFirma).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");

            e.Property(x => x.EstadoProceso)
                .HasConversion<string>()
                .HasColumnType("varchar(30)")
                .HasDefaultValue(EstadoProceso.FIRMADO);

            e.Property(x => x.ClaveIdempotencia).HasColumnType("varchar(100)");
            e.Property(x => x.IPOrigen).HasColumnType("varchar(45)");
            e.Property(x => x.UserAgent).HasMaxLength(300);

            e.HasIndex(x => x.DispositivoId).HasDatabaseName("IX_EntregasDispositivo_Dispositivo");
            e.HasIndex(x => x.EmpleadoId).HasDatabaseName("IX_EntregasDispositivo_Empleado");
            e.HasIndex(x => x.FechaFirma).HasDatabaseName("IX_EntregasDispositivo_Fecha");

            // Un acta reenviada tras un corte de red trae la misma clave: la base la rechaza
            // antes de que se dupliquen el PDF y la sincronización con MobiControl.
            e.HasIndex(x => new { x.EmpresaId, x.ClaveIdempotencia }).IsUnique()
                .HasFilter("[ClaveIdempotencia] IS NOT NULL")
                .HasDatabaseName("UQ_EntregasDispositivo_Idempotencia");

            // Lo consulta el reintento de sincronización, que barre solo las fallidas.
            e.HasIndex(x => x.EstadoProceso).HasDatabaseName("IX_EntregasDispositivo_EstadoProceso");

            e.HasOne(x => x.Dispositivo).WithMany(d => d.Entregas)
                .HasForeignKey(x => x.DispositivoId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Empleado).WithMany(p => p.Entregas)
                .HasForeignKey(x => x.EmpleadoId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Estado).WithMany()
                .HasForeignKey(x => x.EstadoId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Canal).WithMany()
                .HasForeignKey(x => x.CanalId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Distrito).WithMany()
                .HasForeignKey(x => x.DistritoId).OnDelete(DeleteBehavior.NoAction);
        });

        // ---------------- Archivos (referencias, no binarios) ----------------
        builder.Entity<Firma>(e =>
        {
            e.ToTable("Firmas");
            e.HasKey(x => x.FirmaId);
            e.HasIndex(x => x.EntregaId).IsUnique();
            e.Property(x => x.NombreContenedor).HasColumnType("varchar(100)").IsRequired().HasDefaultValue("firmas");
            e.Property(x => x.RutaBlob).HasColumnType("varchar(500)").IsRequired();
            e.Property(x => x.UrlBlob).HasColumnType("varchar(1000)");
            e.Property(x => x.FormatoImagen).HasColumnType("varchar(20)").IsRequired().HasDefaultValue("image/png");
            e.Property(x => x.HashSHA256).HasColumnType("varbinary(32)");
            e.Property(x => x.FechaCaptura).HasDefaultValueSql("SYSUTCDATETIME()");

            e.HasOne(x => x.Entrega).WithOne(x => x.Firma)
                .HasForeignKey<Firma>(x => x.EntregaId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DocumentoPdf>(e =>
        {
            e.ToTable("DocumentosPDF");
            e.HasKey(x => x.DocumentoId);
            e.HasIndex(x => x.EntregaId).IsUnique();
            e.Property(x => x.NombreContenedor).HasColumnType("varchar(100)").IsRequired()
                .HasDefaultValue("documentos-pdf");
            e.Property(x => x.RutaBlob).HasColumnType("varchar(500)").IsRequired();
            e.Property(x => x.UrlBlob).HasColumnType("varchar(1000)");
            e.Property(x => x.NombreArchivo).HasColumnType("varchar(255)").IsRequired()
                .HasDefaultValue("firmaentregadispositivo.pdf");
            e.Property(x => x.HashSHA256).HasColumnType("varbinary(32)");
            e.Property(x => x.FechaGeneracion).HasDefaultValueSql("SYSUTCDATETIME()");

            e.HasOne(x => x.Entrega).WithOne(x => x.DocumentoPdf)
                .HasForeignKey<DocumentoPdf>(x => x.EntregaId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- Integraciones ----------------
        builder.Entity<IntegracionSincronizacion>(e =>
        {
            e.ToTable("IntegracionesSincronizaciones", t => t.HasCheckConstraint(
                "CK_IntegracionesSincronizaciones_Proveedor",
                "[Proveedor] IN ('MOBICONTROL','INFOBIP','GUPSHUP')"));

            e.HasKey(x => x.SincronizacionId);
            e.Property(x => x.Proveedor).HasConversion<string>().HasColumnType("varchar(30)").IsRequired();
            e.Property(x => x.TipoAccion).HasColumnType("varchar(50)").IsRequired();
            e.Property(x => x.FechaEjecucion).HasDefaultValueSql("SYSUTCDATETIME()");

            e.HasIndex(x => x.EntregaId).HasDatabaseName("IX_IntegracionesSync_Entrega");
            e.HasIndex(x => x.Proveedor).HasDatabaseName("IX_IntegracionesSync_Proveedor");

            e.HasOne(x => x.Entrega).WithMany(x => x.Sincronizaciones)
                .HasForeignKey(x => x.EntregaId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IntegracionConfiguracion>(e =>
        {
            e.ToTable("IntegracionesConfiguracion", t =>
            {
                t.HasCheckConstraint("CK_IntegracionesConfiguracion_Proveedor",
                    "[Proveedor] IN ('MOBICONTROL','INFOBIP','GUPSHUP')");
                t.HasCheckConstraint("CK_IntegracionesConfiguracion_TipoAutenticacion",
                    "[TipoAutenticacion] IN ('OAUTH_PASSWORD','API_KEY','BASIC')");
                t.HasCheckConstraint("CK_IntegracionesConfiguracion_ParametrosJson",
                    "[ParametrosAdicionales] IS NULL OR ISJSON([ParametrosAdicionales]) = 1");
            });

            e.HasKey(x => x.ConfiguracionId);
            e.Property(x => x.Proveedor).HasConversion<string>().HasColumnType("varchar(30)").IsRequired();
            e.Property(x => x.Entorno).HasColumnType("varchar(50)").IsRequired().HasDefaultValue("PRODUCCION");
            e.Property(x => x.TipoAutenticacion).HasConversion<string>().HasColumnType("varchar(30)").IsRequired();
            e.Property(x => x.UrlBase).HasColumnType("varchar(300)").IsRequired();
            e.Property(x => x.Activo).HasDefaultValue(true);
            e.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");

            e.HasIndex(x => new { x.EmpresaId, x.Proveedor, x.Entorno })
                .IsUnique().HasDatabaseName("UQ_IntegracionesConfig_Proveedor_Entorno");
        });

        // ---------------- Empresas ----------------
        builder.Entity<Empresa>(e =>
        {
            e.ToTable("Empresas");
            e.HasKey(x => x.EmpresaId);
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
            e.Property(x => x.Nit).HasMaxLength(30);
            e.Property(x => x.CiudadFirma).HasMaxLength(100).IsRequired().HasDefaultValue("Cali");
            e.Property(x => x.Activo).HasDefaultValue(true);

            e.Property(x => x.ApiKeyHash).HasColumnType("varbinary(32)").IsRequired();
            e.Property(x => x.ApiKeyPrefijo).HasMaxLength(12).IsRequired();

            // Por aquí entra cada acta que mandan los equipos: es la consulta más caliente del
            // API y la única forma de resolver la empresa antes de tocar nada más.
            e.HasIndex(x => x.ApiKeyHash).IsUnique().HasDatabaseName("UQ_Empresas_ApiKeyHash");

            e.Property(x => x.MobiControlBaseUrl).HasMaxLength(300);
            e.Property(x => x.MobiControlClientId).HasMaxLength(200);
            e.Property(x => x.MobiControlClientSecret).HasMaxLength(300);
            e.Property(x => x.MobiControlUsuario).HasMaxLength(150);
            e.Property(x => x.MobiControlPassword).HasMaxLength(300);
            e.Property(x => x.MobiControlAtributoFirma).HasMaxLength(100).IsRequired();
            e.Property(x => x.MobiControlAtributoFecha).HasMaxLength(100).IsRequired();
            e.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");

            e.Ignore(x => x.MobiControlConfigurado);
        });

        // ---------------- Aislamiento ----------------
        // Una sola vez, para todo lo que implementa IDeEmpresa: la columna, la llave foránea y
        // el filtro. Hacerlo entidad por entidad invita a que la próxima que se agregue quede
        // fuera y termine siendo visible para todas las empresas.
        ConfigurarPorEmpresa<Distrito>(builder);
        ConfigurarPorEmpresa<Canal>(builder);
        ConfigurarPorEmpresa<EstadoDispositivo>(builder);
        ConfigurarPorEmpresa<Empleado>(builder);
        ConfigurarPorEmpresa<Dispositivo>(builder);
        ConfigurarPorEmpresa<EntregaDispositivo>(builder);
        ConfigurarPorEmpresa<Firma>(builder);
        ConfigurarPorEmpresa<DocumentoPdf>(builder);
        ConfigurarPorEmpresa<IntegracionSincronizacion>(builder);
        ConfigurarPorEmpresa<IntegracionConfiguracion>(builder);
    }

    private void ConfigurarPorEmpresa<T>(ModelBuilder builder) where T : class, IDeEmpresa
    {
        builder.Entity<T>(e =>
        {
            e.Property(x => x.EmpresaId).IsRequired();

            // Restringido y no en cascada: borrar una empresa con actas firmadas destruiría
            // evidencia de entregas. Para sacarla de circulación está el campo Activo.
            e.HasOne(x => x.Empresa)
                .WithMany()
                .HasForeignKey(x => x.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.EmpresaId);
            e.HasQueryFilter(x => SinFiltro || x.EmpresaId == EmpresaFiltro);
        });
    }
}
