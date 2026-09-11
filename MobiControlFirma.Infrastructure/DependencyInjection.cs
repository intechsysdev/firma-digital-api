using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Application.Entregas;
using MobiControlFirma.Infrastructure.Documentos;
using MobiControlFirma.Infrastructure.MobiControl;
using MobiControlFirma.Infrastructure.Persistence;
using MobiControlFirma.Infrastructure.Storage;
using QuestPDF.Infrastructure;

namespace MobiControlFirma.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ---- Base de datos (SQL Server) ----
        var cadenaConexion = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'Default'.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(cadenaConexion, sql =>
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // ---- Almacenamiento de firmas y actas ----
        // Con cadena de Azure va a Blob Storage; sin ella, al disco del servidor. Así el
        // entorno de desarrollo y el servidor propio funcionan sin credenciales de nube.
        services.Configure<AlmacenamientoOptions>(configuration.GetSection(AlmacenamientoOptions.SectionName));
        var cadenaBlob = configuration[$"{AlmacenamientoOptions.SectionName}:AzureBlobConnectionString"];

        if (string.IsNullOrWhiteSpace(cadenaBlob))
            services.AddSingleton<IAlmacenamientoArchivos, AlmacenamientoLocal>();
        else
            services.AddSingleton<IAlmacenamientoArchivos, AlmacenamientoAzureBlob>();

        // ---- Acta en PDF ----
        // Licencia Community de QuestPDF: gratuita para empresas por debajo del umbral de
        // facturación que define su licencia. Se fija aquí porque la librería exige declararla
        // antes de generar el primer documento.
        QuestPDF.Settings.License = LicenseType.Community;
        services.AddSingleton<IGeneradorActaPdf, GeneradorActaPdf>();

        // ---- MobiControl ----
        services.Configure<MobiControlOptions>(configuration.GetSection(MobiControlOptions.SectionName));
        // Sin BaseAddress: cada empresa apunta a su propia consola, así que el cliente compone
        // la URL completa en cada llamada. El plazo real lo pone cada empresa con su propio
        // CancellationToken; este es solo un tope duro para que nada quede colgado para siempre.
        services.AddHttpClient<IClienteMobiControl, ClienteMobiControl>(cliente =>
        {
            cliente.Timeout = TimeSpan.FromMinutes(2);
        });

        // ---- Caso de uso principal ----
        services.AddScoped<IServicioEntregas, ServicioEntregas>();

        return services;
    }
}
