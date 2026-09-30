using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MobiControlFirma.Application.Common;
using MobiControlFirma.Application.Common.Interfaces;

namespace MobiControlFirma.Application.Geolocalizacion;

/// <summary>
/// Un equipo en el mapa: su estado en MobiControl y, si tiene acta, a quién se le entregó.
/// </summary>
/// <param name="EnMobiControl">False si el equipo tiene acta pero ya no está en la consola.</param>
/// <param name="TieneActa">Hay al menos un acta firmada desde este equipo; los datos son de la última.</param>
public record EquipoGeoDto(
    string DeviceId,
    string Nombre,
    string? Plataforma,
    string? Fabricante,
    string? Modelo,
    bool EnMobiControl,
    bool EnLinea,
    int? Bateria,
    bool? Cargando,
    DateTimeOffset? UltimoReporte,
    string? Grupo,
    string? Imei,
    string? Telefono,
    double? Latitud,
    double? Longitud,
    DateTimeOffset? FechaUbicacion,
    double? Velocidad,
    bool TieneActa,
    string? Asociado,
    string? Cedula,
    string? Canal,
    string? Distrito,
    string? EstadoEquipo,
    DateTimeOffset? FechaActa,
    Guid? EntregaUid);

public record FlotaDto(IReadOnlyList<EquipoGeoDto> Equipos, DateTimeOffset Consultado);

public record PuntoRecorridoDto(double Latitud, double Longitud, DateTimeOffset Momento, double? Velocidad, double? Rumbo);

public record ConfiguracionGeoDto(bool MobiControlConfigurado, string? GoogleMapsApiKey);

public interface IServicioGeolocalizacion
{
    Task<ConfiguracionGeoDto> ConfiguracionAsync(CancellationToken ct = default);
    Task<FlotaDto> ListarAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PuntoRecorridoDto>> RecorridoAsync(string deviceId, DateOnly fecha, CancellationToken ct = default);
    Task LocalizarAsync(string deviceId, CancellationToken ct = default);
}

/// <summary>
/// Geolocalización de la flota: MobiControl dice dónde está cada equipo y si está en línea; las
/// actas firmadas dicen de quién es. Es lo que en Seguimiento Online salía del ERP.
/// </summary>
public class ServicioGeolocalizacion(
    IApplicationDbContext db,
    IContextoEmpresa empresa,
    IProveedorConfiguracion configuracion,
    IClienteMobiControl mobiControl,
    ILogger<ServicioGeolocalizacion> logger) : IServicioGeolocalizacion
{
    /// <summary>
    /// Consultas de ubicación simultáneas. La consola dejó de responder cuando se le pidió un
    /// recorrido por cada equipo seguido: con un tope se reparte la carga sin ahogarla.
    /// </summary>
    private const int ConsultasSimultaneas = 6;

    /// <summary>Colombia no tiene horario de verano: el día del recorrido es siempre de -05:00 a -05:00.</summary>
    private static readonly TimeZoneInfo ZonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    public async Task<ConfiguracionGeoDto> ConfiguracionAsync(CancellationToken ct = default)
    {
        var config = await configuracion.ObtenerAsync(EmpresaRequerida(), ct);
        return new ConfiguracionGeoDto(config?.MobiControlConfigurado ?? false, config?.GoogleMapsApiKey);
    }

    public async Task<FlotaDto> ListarAsync(CancellationToken ct = default)
    {
        EmpresaRequerida();

        var equipos = await mobiControl.ListarEquiposAsync(ct);
        var actas = await UltimaActaPorEquipoAsync(ct);

        // La posición se pide solo a teléfonos y tabletas, y en paralelo con tope. Un equipo que
        // no contesta sale sin punto en el mapa; no tumba la consulta de los demás.
        var ubicaciones = new Dictionary<string, UbicacionMobiControl?>(StringComparer.OrdinalIgnoreCase);
        using (var cupo = new SemaphoreSlim(ConsultasSimultaneas))
        {
            var tareas = equipos.Where(e => e.TieneGps).Select(async equipo =>
            {
                await cupo.WaitAsync(ct);
                try
                {
                    var ubicacion = await mobiControl.UltimaUbicacionAsync(equipo.DeviceId, ct);
                    lock (ubicaciones) ubicaciones[equipo.DeviceId] = ubicacion;
                }
                catch (ErrorSolicitudException ex)
                {
                    logger.LogWarning("Sin última posición para {Equipo}: {Motivo}", equipo.DeviceId, ex.Message);
                }
                finally
                {
                    cupo.Release();
                }
            });

            await Task.WhenAll(tareas);
        }

        var salida = new List<EquipoGeoDto>(equipos.Count + actas.Count);

        foreach (var equipo in equipos)
        {
            actas.TryGetValue(equipo.DeviceId, out var acta);
            ubicaciones.TryGetValue(equipo.DeviceId, out var ubicacion);
            salida.Add(AVista(equipo, acta, ubicacion));
        }

        // Equipos con acta que ya no están en la consola: se listan igual, porque que un equipo
        // entregado haya desaparecido de MobiControl es justamente algo que hay que ver.
        var enConsola = equipos.Select(e => e.DeviceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var acta in actas.Values.Where(a => !enConsola.Contains(a.DeviceId)))
            salida.Add(AVista(null, acta, null));

        return new FlotaDto(
            [.. salida.OrderByDescending(e => e.EnLinea).ThenBy(e => e.Asociado ?? e.Nombre, StringComparer.CurrentCultureIgnoreCase)],
            DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<PuntoRecorridoDto>> RecorridoAsync(
        string deviceId, DateOnly fecha, CancellationToken ct = default)
    {
        EmpresaRequerida();

        var inicio = fecha.ToDateTime(TimeOnly.MinValue);
        var desde = new DateTimeOffset(inicio, ZonaHoraria.GetUtcOffset(inicio));
        var hasta = desde.AddDays(1).AddSeconds(-1);

        var puntos = await mobiControl.RecorridoAsync(deviceId, desde, hasta, ct);
        return [.. puntos.Select(p => new PuntoRecorridoDto(p.Latitud, p.Longitud, p.Momento, p.Velocidad, p.Rumbo))];
    }

    public async Task LocalizarAsync(string deviceId, CancellationToken ct = default)
    {
        EmpresaRequerida();
        await mobiControl.LocalizarAsync(deviceId, ct);
    }

    // ------------------------------------------------------------------------------------

    private int EmpresaRequerida() =>
        empresa.EmpresaId
        ?? throw new ErrorSolicitudException("Elige una empresa para ver la ubicación de sus equipos.");

    private sealed record ActaEquipo(
        string DeviceId, string? Fabricante, string? Modelo, string? Imei,
        string Asociado, string Cedula, string? Canal, string? Distrito, string? Estado,
        DateTime FechaFirma, Guid EntregaUid);

    /// <summary>
    /// La última acta de cada equipo. Se ordena en la base y se agrupa en memoria: son unas
    /// pocas filas por equipo, y así la consulta no depende de cómo traduzca EF un GroupBy.
    /// </summary>
    private async Task<Dictionary<string, ActaEquipo>> UltimaActaPorEquipoAsync(CancellationToken ct)
    {
        var filas = await db.Entregas.AsNoTracking()
            .OrderByDescending(e => e.FechaFirma)
            .Select(e => new ActaEquipo(
                e.Dispositivo.MobiControlDeviceId,
                e.Dispositivo.Fabricante,
                e.Dispositivo.Modelo,
                e.Dispositivo.IMEI,
                e.NombreAsociadoFirmante,
                e.Empleado.Cedula,
                e.Canal != null ? e.Canal.Nombre : null,
                e.Distrito != null ? e.Distrito.Nombre : null,
                e.Estado != null ? e.Estado.Nombre : null,
                e.FechaFirma,
                e.EntregaUid))
            .ToListAsync(ct);

        var porEquipo = new Dictionary<string, ActaEquipo>(StringComparer.OrdinalIgnoreCase);
        foreach (var fila in filas)
            porEquipo.TryAdd(fila.DeviceId, fila);

        return porEquipo;
    }

    private static EquipoGeoDto AVista(EquipoMobiControl? equipo, ActaEquipo? acta, UbicacionMobiControl? ubicacion) =>
        new(
            equipo?.DeviceId ?? acta!.DeviceId,
            equipo?.Nombre ?? acta!.DeviceId,
            equipo?.Plataforma,
            equipo?.Fabricante ?? acta?.Fabricante,
            equipo?.Modelo ?? acta?.Modelo,
            equipo is not null,
            equipo?.EnLinea ?? false,
            equipo?.Bateria,
            equipo?.Cargando,
            equipo?.UltimoReporte,
            equipo?.Grupo,
            equipo?.Imei ?? acta?.Imei,
            equipo?.Telefono,
            ubicacion?.Latitud,
            ubicacion?.Longitud,
            ubicacion?.Momento,
            ubicacion?.Velocidad,
            acta is not null,
            acta?.Asociado,
            acta?.Cedula,
            acta?.Canal,
            acta?.Distrito,
            acta?.Estado,
            acta is null ? null : new DateTimeOffset(DateTime.SpecifyKind(acta.FechaFirma, DateTimeKind.Utc)),
            acta?.EntregaUid);
}
