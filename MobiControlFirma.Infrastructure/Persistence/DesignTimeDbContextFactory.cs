using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MobiControlFirma.Infrastructure.Persistence;

/// <summary>
/// Contexto para las herramientas de EF (<c>dotnet ef migrations</c>), que corren sin arrancar
/// la aplicación. Lee la misma cadena de conexión del API para no tener dos verdades.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var rutaApi = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(), "..", "MobiControlFirma.API"));

        var configuracion = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(rutaApi) ? rutaApi : Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cadena = configuracion.GetConnectionString("Default")
            ?? "Server=JEFO-PC;Database=mobicontrol_firmas_db;Trusted_Connection=True;TrustServerCertificate=True";

        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(cadena)
            .Options;

        return new ApplicationDbContext(opciones);
    }
}
