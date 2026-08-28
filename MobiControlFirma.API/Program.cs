using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using MobiControlFirma.API.Configuration;
using MobiControlFirma.Application.Common;
using MobiControlFirma.Infrastructure;
using MobiControlFirma.Infrastructure.MobiControl;
using MobiControlFirma.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

const string PoliticaCors = "MobiControlFirmaCors";
const string LimiteFirmas = "firmas";

// --- Opciones ---
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<SeguridadOptions>(builder.Configuration.GetSection(SeguridadOptions.SectionName));

var appOptions = builder.Configuration.GetSection(AppOptions.SectionName).Get<AppOptions>() ?? new AppOptions();
var seguridad = builder.Configuration.GetSection(SeguridadOptions.SectionName).Get<SeguridadOptions>()
    ?? new SeguridadOptions();

// Sin llaves configuradas el API queda abierto a internet: cualquiera podría registrar actas
// falsas o leer el histórico completo de cédulas y firmas. En producción arranca reventando.
if (builder.Environment.IsProduction() &&
    (string.IsNullOrWhiteSpace(seguridad.ApiKeyDispositivo) ||
     string.IsNullOrWhiteSpace(seguridad.ApiKeyAdministrador)))
{
    throw new InvalidOperationException(
        "Faltan 'Seguridad:ApiKeyDispositivo' y/o 'Seguridad:ApiKeyAdministrador'. " +
        "Sin ellas el API quedaría sin autenticación.");
}

// --- Controladores + JSON (enums como texto) ---
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

// --- Infraestructura (base de datos, almacenamiento, PDF, MobiControl) ---
builder.Services.AddInfrastructure(builder.Configuration);

// --- CORS ---
// El formulario se instala en el equipo y el navegador lo abre desde el sistema de archivos,
// así que su Origin llega como "null" y ninguna lista blanca lo cubre. Con la lista vacía se
// permite cualquier origen: la autenticación es por cabecera, no por cookie, de modo que un
// sitio ajeno no gana nada al invocarlo sin la llave.
builder.Services.AddCors(options => options.AddPolicy(PoliticaCors, politica =>
{
    if (appOptions.CorsOrigins.Length == 0)
        politica.AllowAnyOrigin();
    else
        politica.SetIsOriginAllowed(origen =>
            origen == "null" || appOptions.CorsOrigins.Contains(origen, StringComparer.OrdinalIgnoreCase));

    politica.AllowAnyHeader().AllowAnyMethod();
}));

// --- Límite de peticiones ---
// Registrar un acta cuesta un PDF y tres llamadas a MobiControl. Un equipo con el formulario
// en bucle podría saturar el servidor sin querer, así que se acota por IP.
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddPolicy(LimiteFirmas, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// --- Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MobiControl Firma API",
        Version = "v1",
        Description = "Actas de entrega de dispositivos móviles firmadas desde el equipo.",
    });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = ApiKeyAttribute.NombreCabecera,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Llave de acceso (Seguridad:ApiKeyDispositivo o Seguridad:ApiKeyAdministrador).",
    });

    c.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("ApiKey", documento)] = [],
    });
});

var app = builder.Build();

// --- Migración + datos base ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var mobiControl = scope.ServiceProvider.GetRequiredService<IOptions<MobiControlOptions>>().Value;
    await ApplicationDbContextSeed.SeedAsync(db, mobiControl.BaseUrl);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(PoliticaCors);

// Errores: los de validación salen como 400 con el mensaje que el formulario muestra tal cual;
// el resto se registra y devuelve 500. Va después de UseCors para que la respuesta de error
// también lleve las cabeceras y el navegador pueda leerla en vez de reportar un fallo de red.
app.Use(async (contexto, siguiente) =>
{
    try
    {
        await siguiente();
    }
    catch (ErrorSolicitudException ex)
    {
        if (!contexto.Response.HasStarted)
        {
            contexto.Response.Clear();
            contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
            await contexto.Response.WriteAsJsonAsync(new { message = ex.Message });
        }
    }
    catch (Exception ex)
    {
        var logger = contexto.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error no controlado en {Ruta}", contexto.Request.Path);

        if (!contexto.Response.HasStarted)
        {
            contexto.Response.Clear();
            contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var mensaje = ex.InnerException?.Message ?? ex.Message;
            await contexto.Response.WriteAsJsonAsync(new { message = mensaje });
        }
    }
});

app.UseRateLimiter();

app.MapControllers().RequireRateLimiting(LimiteFirmas);

app.Run();
