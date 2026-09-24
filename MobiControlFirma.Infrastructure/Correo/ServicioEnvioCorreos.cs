using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Domain.Entities;
using MobiControlFirma.Domain.Enums;
using MobiControlFirma.Infrastructure.Persistence;

namespace MobiControlFirma.Infrastructure.Correo;

/// <summary>
/// Vacía la bandeja de salida de copias del acta. Corre en segundo plano para que el envío no
/// esté en el camino crítico de una firma.
/// </summary>
public class ServicioEnvioCorreos(
    IServiceScopeFactory fabricaAmbitos,
    ILogger<ServicioEnvioCorreos> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);

    /// <summary>Espera creciente entre intentos. Al quinto fallo se deja de insistir.</summary>
    private static readonly TimeSpan[] Esperas =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
    ];

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
                logger.LogError(ex, "Fallo procesando la bandeja de correos.");
            }

            try { await Task.Delay(Intervalo, ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcesarTandaAsync(CancellationToken ct)
    {
        using var ambito = fabricaAmbitos.CreateScope();
        var db = ambito.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var enviador = ambito.ServiceProvider.GetRequiredService<IEnviadorCorreo>();
        var configuracion = ambito.ServiceProvider.GetRequiredService<IProveedorConfiguracion>();
        var almacenamiento = ambito.ServiceProvider.GetRequiredService<IAlmacenamientoArchivos>();

        var ahora = DateTime.UtcNow;

        // IgnoreQueryFilters no es un atajo: esto corre fuera de una petición, así que no hay
        // empresa en contexto y el filtro global dejaría la consulta vacía para siempre. El
        // aislamiento se mantiene igual porque cada envío ya trae su empresa decidida.
        var pendientes = await db.EnviosCorreo
            .IgnoreQueryFilters()
            .Where(e => e.Estado == EstadoEnvioCorreo.PENDIENTE
                     || (e.Estado == EstadoEnvioCorreo.ERROR && e.ProximoIntento != null && e.ProximoIntento <= ahora))
            .OrderBy(e => e.EnvioId)
            .Take(20)
            .ToListAsync(ct);

        if (pendientes.Count == 0) return;

        foreach (var envio in pendientes)
            await ProcesarAsync(db, enviador, configuracion, almacenamiento, envio, ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task ProcesarAsync(
        ApplicationDbContext db,
        IEnviadorCorreo enviador,
        IProveedorConfiguracion configuracion,
        IAlmacenamientoArchivos almacenamiento,
        EnvioCorreo envio,
        CancellationToken ct)
    {
        envio.Intentos++;

        // La configuración es de One, no de la base local. Corre fuera de una petición, así que
        // se pide por el EmpresaId que el envío ya trae decidido.
        var config = await configuracion.ObtenerAsync(envio.EmpresaId, ct);

        var entrega = await db.Entregas.IgnoreQueryFilters().AsNoTracking()
            .Include(e => e.DocumentoPdf)
            .Include(e => e.Dispositivo)
            .FirstOrDefaultAsync(e => e.EntregaId == envio.EntregaId, ct);

        if (config is null || entrega?.DocumentoPdf is null)
        {
            Marcar(envio, false, config is null
                ? "No se pudo obtener de One la configuración de la empresa."
                : "No se encontró el acta o su documento.");
            return;
        }

        var pdf = await almacenamiento.LeerAsync(
            entrega.DocumentoPdf.NombreContenedor, entrega.DocumentoPdf.RutaBlob, ct);

        if (pdf is null)
        {
            Marcar(envio, false, "El archivo del acta no está en el almacenamiento.");
            return;
        }

        var destinatarios = envio.Destinatarios
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var resultado = await enviador.EnviarActaAsync(
            config, destinatarios, envio.Asunto,
            CuerpoHtml(config, entrega), entrega.DocumentoPdf.NombreArchivo, pdf, ct);

        Marcar(envio, resultado.Exitoso, resultado.Detalle);

        if (resultado.Exitoso)
            logger.LogInformation("Copia del acta {Acta} enviada a {Cuantos} destinatario(s).",
                entrega.EntregaUid, destinatarios.Length);
    }

    private void Marcar(EnvioCorreo envio, bool exitoso, string? detalle)
    {
        if (exitoso)
        {
            envio.Estado = EstadoEnvioCorreo.ENVIADO;
            envio.FechaEnvio = DateTime.UtcNow;
            envio.UltimoError = null;
            envio.ProximoIntento = null;
            return;
        }

        envio.UltimoError = detalle?[..Math.Min(1000, detalle.Length)];

        if (envio.Intentos >= Esperas.Length)
        {
            // Se deja de insistir, pero no se borra: queda visible en la consola para que
            // alguien decida si lo reintenta a mano o si el destinatario estaba mal.
            envio.Estado = EstadoEnvioCorreo.DESCARTADO;
            envio.ProximoIntento = null;

            logger.LogWarning("Se agotaron los intentos de la copia {Envio}: {Detalle}", envio.EnvioId, detalle);
            return;
        }

        envio.Estado = EstadoEnvioCorreo.ERROR;
        envio.ProximoIntento = DateTime.UtcNow.Add(Esperas[envio.Intentos - 1]);
    }

    private static string CuerpoHtml(ConfiguracionEmpresa config, EntregaDispositivo entrega)
    {
        var equipo = string.Join(" ", new[] { entrega.Dispositivo?.Fabricante, entrega.Dispositivo?.Modelo }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        return $"""
            <p>Adjuntamos el acta de entrega firmada.</p>
            <table style="border-collapse:collapse;font-family:sans-serif;font-size:14px">
              <tr><td style="padding:3px 12px 3px 0;color:#5b6a80">Acta</td><td><b>{entrega.EntregaUid.ToString()[..8].ToUpperInvariant()}</b></td></tr>
              <tr><td style="padding:3px 12px 3px 0;color:#5b6a80">Asociado</td><td>{entrega.NombreAsociadoFirmante}</td></tr>
              <tr><td style="padding:3px 12px 3px 0;color:#5b6a80">Equipo</td><td>{(string.IsNullOrWhiteSpace(equipo) ? "-" : equipo)}</td></tr>
              <tr><td style="padding:3px 12px 3px 0;color:#5b6a80">Fecha</td><td>{entrega.FechaFirma:yyyy-MM-dd HH:mm}</td></tr>
            </table>
            <p style="color:#5b6a80;font-size:12px">{config.TenantNombre} · Mensaje automático, no responder.</p>
            """;
    }
}
