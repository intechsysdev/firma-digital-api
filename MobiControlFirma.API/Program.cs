using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using MobiControlFirma.API.Configuration;
using MobiControlFirma.Application.Common;
using MobiControlFirma.Infrastructure;
using MobiControlFirma.Infrastructure.Identidad;
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

// --- Usuarios de la consola (ASP.NET Identity sobre la misma base) ---
// Los endpoints traen su propio esquema de token bearer, así que el front no maneja cookies
// y puede vivir en cualquier origen sin depender de CSRF.
builder.Services.AddAuthorization();
builder.Services
    .AddIdentityApiEndpoints<UsuarioAdmin>(opciones =>
    {
        opciones.User.RequireUniqueEmail = true;
        opciones.Password.RequiredLength = 8;
        // Bloqueo tras intentos fallidos: el API está expuesto a internet y la única barrera
        // contra fuerza bruta sería el límite por IP, que un atacante rota sin esfuerzo.
        opciones.Lockout.MaxFailedAccessAttempts = 5;
        opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

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

    // Usuario inicial de la consola. Solo cuando la tabla está vacía: si ya hay usuarios, este
    // bloque no toca nada, así que cambiar la contraseña en la consola no la revierte el
    // siguiente despliegue.
    if (!string.IsNullOrWhiteSpace(seguridad.UsuarioInicial) &&
        !string.IsNullOrWhiteSpace(seguridad.ClaveInicial))
    {
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioAdmin>>();

        if (!usuarios.Users.Any())
        {
            var inicial = new UsuarioAdmin
            {
                UserName = seguridad.UsuarioInicial,
                Email = seguridad.UsuarioInicial,
                EmailConfirmed = true,
                NombreCompleto = "Administrador",
            };

            var creado = await usuarios.CreateAsync(inicial, seguridad.ClaveInicial);
            var registro = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            if (creado.Succeeded)
                registro.LogInformation("Usuario inicial {Correo} creado.", seguridad.UsuarioInicial);
            else
                registro.LogError("No se pudo crear el usuario inicial: {Errores}",
                    string.Join("; ", creado.Errors.Select(e => e.Description)));
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(PoliticaCors);

// El front de React se publica dentro de wwwroot y se sirve desde este mismo App Service:
// mismo origen que el API, así que no hay CORS ni un recurso extra que desplegar.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

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

var cuenta = app.MapGroup("/api/v1/cuenta").WithTags("Cuenta").RequireRateLimiting(LimiteFirmas);
cuenta.MapIdentityApi<UsuarioAdmin>();

// MapIdentityApi publica /register sin autenticación: tal cual, cualquiera en internet podría
// crearse un usuario y entrar a la consola. Se exige estar dentro para dar de alta a otro.
cuenta.AddEndpointFilter(async (contexto, siguiente) =>
{
    var ruta = contexto.HttpContext.Request.Path.Value ?? string.Empty;

    if (ruta.EndsWith("/register", StringComparison.OrdinalIgnoreCase) &&
        !ApiKeyAttribute.EsAdministrador(contexto.HttpContext))
    {
        return Results.Json(
            new { message = "Solo un usuario autenticado puede registrar usuarios nuevos." },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    return await siguiente(contexto);
});

// Cualquier ruta que no sea del API la resuelve el front: React Router necesita que
// /entregas/algo devuelva el index en vez de un 404 del servidor.
app.MapFallback(async contexto =>
{
    var indice = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");

    if (contexto.Request.Path.StartsWithSegments("/api") || !File.Exists(indice))
    {
        contexto.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    contexto.Response.ContentType = "text/html";
    await contexto.Response.SendFileAsync(indice);
});

app.Run();
