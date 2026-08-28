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
        services.AddHttpClient<IClienteMobiControl, ClienteMobiControl>((sp, cliente) =>
        {
            var opciones = sp.GetRequiredService<IOptions<MobiControlOptions>>().Value;

            if (!string.IsNullOrWhiteSpace(opciones.BaseUrl))
            {
                // La barra final es obligatoria: sin ella, BaseAddress descarta el último
                // segmento de la ruta y las peticiones salen a /api/token en la raíz del host.
                var baseUrl = opciones.BaseUrl.TrimEnd('/') + "/";
                cliente.BaseAddress = new Uri(baseUrl);
            }

            // El asociado está esperando con el equipo en la mano: si la consola no responde,
            // vale más cerrar el acta y reintentar la sincronización después que dejarlo colgado.
            cliente.Timeout = TimeSpan.FromSeconds(Math.Max(5, opciones.TimeoutSegundos));
        });

        // ---- Caso de uso principal ----
        services.AddScoped<IServicioEntregas, ServicioEntregas>();

        return services;
    }
}
