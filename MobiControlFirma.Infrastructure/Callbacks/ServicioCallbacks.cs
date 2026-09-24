using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Domain.Entities;
using MobiControlFirma.Domain.Enums;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.Infrastructure.Callbacks;

/// <summary>
/// Avisa al sistema de origen que una solicitud de firma se completó. Corre en segundo plano,
/// igual que la copia por correo: el asociado ya firmó y no tiene por qué esperar a que el
/// origen conteste, ni ver un error si el origen está caído.
///
/// La URL y el secreto son de cada empresa y viven en One (<c>CALLBACK_URL</c>,
/// <c>CALLBACK_SECRET</c>). Con secreto, cada aviso va firmado con HMAC-SHA256 sobre
/// <c>"{timestamp}.{cuerpo}"</c> para que el origen pueda comprobar que salió de aquí y que
/// nadie lo reenvió después.
/// </summary>
public class ServicioCallbacks(
    IServiceScopeFactory fabricaAmbitos,
    IHttpClientFactory clientes,
    ILogger<ServicioCallbacks> logger) : BackgroundService
{
    /// <summary>Cliente HTTP de los avisos. Sin BaseAddress: cada empresa tiene su URL.</summary>
    public const string ClienteHttp = "callbacks";

    public const string Evento = "firma.completada";

    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(20);

    /// <summary>Espera creciente entre intentos. Al quinto fallo se deja de insistir.</summary>
    private static readonly TimeSpan[] Esperas =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcesarTandaAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Nunca se deja caer el bucle: si una vuelta falla, la siguiente lo reintenta.
                logger.LogError(ex, "Fallo procesando la bandeja de callbacks.");
            }

            try { await Task.Delay(Intervalo, ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcesarTandaAsync(CancellationToken ct)
    {
        using var ambito = fabricaAmbitos.CreateScope();
        var db = ambito.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var configuracion = ambito.ServiceProvider.GetRequiredService<IProveedorConfiguracion>();
        var almacenamiento = ambito.ServiceProvider.GetRequiredService<IAlmacenamientoArchivos>();

        var ahora = DateTime.UtcNow;

        // Fuera de una petición no hay empresa en contexto: sin IgnoreQueryFilters la consulta
        // saldría vacía siempre. Cada solicitud ya trae su empresa decidida.
        var pendientes = await db.Solicitudes
            .IgnoreQueryFilters()
            .Include(s => s.Empresa)
            .Where(s => s.EstadoCallback == EstadoCallback.PENDIENTE
                     || (s.EstadoCallback == EstadoCallback.ERROR
                         && s.ProximoIntentoCallback != null && s.ProximoIntentoCallback <= ahora))
            .OrderBy(s => s.SolicitudId)
            .Take(10)
            .ToListAsync(ct);

        foreach (var solicitud in pendientes)
        {
            await ProcesarAsync(db, configuracion, almacenamiento, solicitud, ct);

            // Se guarda uno por uno: si el origen de la siguiente tarda hasta el timeout, el
            // resultado de esta ya quedó escrito y no se vuelve a avisar dos veces.
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task ProcesarAsync(
        ApplicationDbContext db,
        IProveedorConfiguracion configuracion,
        IAlmacenamientoArchivos almacenamiento,
        SolicitudFirma solicitud,
        CancellationToken ct)
    {
        solicitud.IntentosCallback++;

        var config = await configuracion.ObtenerAsync(solicitud.EmpresaId, ct);

        if (config is null)
        {
            Marcar(solicitud, false, null, "No se pudo obtener de One la configuración de la empresa.");
            return;
        }

        if (!config.CallbackConfigurado)
        {
            Marcar(solicitud, false, null, "La empresa no tiene CALLBACK_URL configurada en One.");
            return;
        }

        var entrega = await db.Entregas.IgnoreQueryFilters().AsNoTracking()
            .Include(e => e.Empleado)
            .Include(e => e.Dispositivo)
            .Include(e => e.Estado)
            .Include(e => e.Canal)
            .Include(e => e.Distrito)
            .Include(e => e.DocumentoPdf)
            .FirstOrDefaultAsync(e => e.EntregaId == solicitud.EntregaId, ct);

        if (entrega?.DocumentoPdf is null)
        {
            Marcar(solicitud, false, null, "No se encontró el acta o su documento.");
            return;
        }

        var pdf = await almacenamiento.LeerAsync(
            entrega.DocumentoPdf.NombreContenedor, entrega.DocumentoPdf.RutaBlob, ct);

        if (pdf is null)
        {
            Marcar(solicitud, false, null, "El archivo del acta no está en el almacenamiento.");
            return;
        }

        var cuerpo = JsonSerializer.Serialize(Carga(solicitud, entrega, pdf), Json);
        var marcaTiempo = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        using var peticion = new HttpRequestMessage(HttpMethod.Post, config.CallbackUrl)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json"),
        };

        peticion.Headers.Add("X-Firma-Evento", Evento);
        // Estable entre reintentos: el origen puede usarlo para no procesar dos veces el mismo aviso.
        peticion.Headers.Add("X-Firma-Id", solicitud.SolicitudUid.ToString());
        peticion.Headers.Add("X-Firma-Timestamp", marcaTiempo);

        if (!string.IsNullOrWhiteSpace(config.CallbackSecreto))
            peticion.Headers.Add("X-Firma-Signature", "sha256=" + Firmar(config.CallbackSecreto, marcaTiempo, cuerpo));

        peticion.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var http = clientes.CreateClient(ClienteHttp);
            using var respuesta = await http.SendAsync(peticion, ct);

            if (respuesta.IsSuccessStatusCode)
            {
                Marcar(solicitud, true, (int)respuesta.StatusCode, null);
                logger.LogInformation("Callback de la solicitud {Origen} entregado ({Codigo}).",
                    solicitud.IdSolicitudOrigen, (int)respuesta.StatusCode);
                return;
            }

            var detalle = await respuesta.Content.ReadAsStringAsync(ct);
            Marcar(solicitud, false, (int)respuesta.StatusCode,
                $"El origen respondió {(int)respuesta.StatusCode}: {detalle[..Math.Min(500, detalle.Length)]}");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Marcar(solicitud, false, null, ex.Message);
        }
    }

    /// <summary>
    /// Lo que recibe el origen. Lleva el PDF adentro a propósito: el origen no tiene credencial
    /// para descargarlo de este API, y con el documento en el aviso no necesita ninguna.
    /// </summary>
    private static object Carga(SolicitudFirma solicitud, EntregaDispositivo entrega, byte[] pdf) => new
    {
        evento = Evento,
        idSolicitud = solicitud.IdSolicitudOrigen,
        solicitudUid = solicitud.SolicitudUid,
        empresa = new
        {
            oneTenantId = solicitud.Empresa.OneTenantId,
            slug = solicitud.Empresa.OneSlug,
            nombre = solicitud.Empresa.Nombre,
        },
        acta = new
        {
            entregaUid = entrega.EntregaUid,
            numero = entrega.EntregaUid.ToString()[..8].ToUpperInvariant(),
            // La base guarda UTC pero lo devuelve sin marcar; sin la Z el origen lo leería en su hora local.
            fechaFirma = DateTime.SpecifyKind(entrega.FechaFirma, DateTimeKind.Utc),
            ciudadFirma = entrega.CiudadFirma,
            estadoProceso = entrega.EstadoProceso.ToString(),
            firmante = new
            {
                nombre = entrega.NombreAsociadoFirmante,
                nombreTenedor = entrega.Empleado.NombreCompleto,
                cedula = entrega.Empleado.Cedula,
                correo = entrega.CorreoAsociado,
            },
            equipo = new
            {
                deviceId = entrega.Dispositivo.MobiControlDeviceId,
                fabricante = entrega.Dispositivo.Fabricante,
                modelo = entrega.Dispositivo.Modelo,
                imei = entrega.Dispositivo.IMEI,
                iccid = entrega.ICCID,
                numeroCelular = entrega.NumeroCelular,
                estado = entrega.Estado?.Nombre,
                canal = entrega.Canal?.Nombre,
                distrito = entrega.Distrito?.Nombre,
                costo = entrega.CostoEquipo,
                entregables = entrega.Entregables,
            },
        },
        documento = new
        {
            nombreArchivo = entrega.DocumentoPdf!.NombreArchivo,
            tipoContenido = "application/pdf",
            sha256 = entrega.DocumentoPdf.HashSHA256 is { } hash ? Convert.ToHexStringLower(hash) : null,
            base64 = Convert.ToBase64String(pdf),
        },
    };

    private static string Firmar(string secreto, string marcaTiempo, string cuerpo) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secreto),
            Encoding.UTF8.GetBytes($"{marcaTiempo}.{cuerpo}")));

    private void Marcar(SolicitudFirma solicitud, bool exitoso, int? codigo, string? detalle)
    {
        solicitud.CodigoHttpCallback = codigo;

        if (exitoso)
        {
            solicitud.EstadoCallback = EstadoCallback.ENVIADO;
            solicitud.FechaCallback = DateTime.UtcNow;
            solicitud.UltimoErrorCallback = null;
            solicitud.ProximoIntentoCallback = null;
            return;
        }

        solicitud.UltimoErrorCallback = detalle?[..Math.Min(1000, detalle.Length)];

        if (solicitud.IntentosCallback >= Esperas.Length)
        {
            // Se deja de insistir, pero queda registrado: el reintento manual lo vuelve a poner
            // en cola cuando se corrija lo que fallaba.
            solicitud.EstadoCallback = EstadoCallback.DESCARTADO;
            solicitud.ProximoIntentoCallback = null;

            logger.LogWarning("Se agotaron los intentos del callback de {Origen}: {Detalle}",
                solicitud.IdSolicitudOrigen, detalle);
            return;
        }

        solicitud.EstadoCallback = EstadoCallback.ERROR;
        solicitud.ProximoIntentoCallback = DateTime.UtcNow.Add(Esperas[solicitud.IntentosCallback - 1]);
    }
}
