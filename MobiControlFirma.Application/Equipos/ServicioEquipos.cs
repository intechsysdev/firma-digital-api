using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MobiControlFirma.Application.Atributos;
using MobiControlFirma.Application.Common;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Application.Solicitudes;
using MobiControlFirma.Domain.Entities;
using MobiControlFirma.Domain.Enums;

namespace MobiControlFirma.Application.Equipos;

/// <summary>La última acta firmada de un equipo.</summary>
public record ActaEquipoDto(Guid EntregaUid, string Asociado, string Cedula, DateTime FechaFirma);

/// <summary>Un enlace de firma que todavía espera al asociado.</summary>
/// <param name="CreadaPor">Correo de quien lo creó en la consola; null si lo pidió el sistema de origen.</param>
public record EnlacePendienteDto(Guid SolicitudUid, string UrlFirma, DateTime FechaVencimiento, string? Correo, string? CreadaPor);

/// <summary>Un equipo de la consola de MobiControl y en qué va su acta.</summary>
public record EquipoConsolaDto(
    string DeviceId,
    string Nombre,
    string? Plataforma,
    string? Fabricante,
    string? Modelo,
    string? Imei,
    string? Serial,
    string? Telefono,
    string? Grupo,
    bool EnLinea,
    DateTimeOffset? UltimoReporte,
    ActaEquipoDto? UltimaActa,
    EnlacePendienteDto? EnlacePendiente);

public record ListaEquiposDto(IReadOnlyList<EquipoConsolaDto> Equipos, bool MobiControlConfigurado, DateTimeOffset Consultado);

/// <summary>Los datos del acta, tal como se precargan y se editan en la consola.</summary>
public class DatosActaConsola
{
    [MaxLength(20)]  public string? Cedula { get; set; }
    /// <summary>Nombre del asociado que recibe el equipo; en el equipo venía del atributo <c>Usuario</c>.</summary>
    [MaxLength(200)] public string? Usuario { get; set; }
    [MaxLength(200)] public string? Correo { get; set; }
    [MaxLength(30)]  public string? TipoDispositivo { get; set; }
    [MaxLength(100)] public string? Serial { get; set; }
    [MaxLength(100)] public string? Fabricante { get; set; }
    [MaxLength(100)] public string? Modelo { get; set; }
    [MaxLength(50)]  public string? Imei { get; set; }
    [MaxLength(50)]  public string? Iccid { get; set; }
    [MaxLength(30)]  public string? NumeroCelular { get; set; }
    [MaxLength(50)]  public string? Estado { get; set; }
    [MaxLength(100)] public string? Canal { get; set; }
    [MaxLength(100)] public string? Distrito { get; set; }
    [MaxLength(50)]  public string? Costo { get; set; }
    public string? Entregables { get; set; }
    [MaxLength(100)] public string? CiudadFirma { get; set; }
}

/// <summary>Un campo del acta que sale de un atributo de MobiControl, con el tipo que tiene allá.</summary>
/// <param name="Campo">Nombre del campo en el formulario (cedula, estado…).</param>
/// <param name="Tipo">Como lo nombra MobiControl: Text, Enumerator, Float, Boolean, Date.</param>
/// <param name="Opciones">Solo para las listas (Enumerator).</param>
public record CampoMobiControlDto(string Campo, string Atributo, string Tipo, IReadOnlyList<string> Opciones);

/// <param name="Datos">Lo que se precarga en el formulario: lo que MobiControl sabe del equipo.</param>
/// <param name="AtributosEnMobiControl">
/// Campos del formulario (cedula, usuario…) que salen de un atributo que existe en la consola. Son
/// los únicos que se pueden devolver a MobiControl si se corrigen.
/// </param>
/// <param name="Campos">Tipo y opciones de cada campo con atributo, para mostrar listas y números como tales.</param>
/// <param name="PuedeActualizarMobiControl">Dueño o administrador: puede escribir atributos en MobiControl.</param>
/// <param name="CorreoConfigurado">La empresa puede mandar el enlace por correo.</param>
public record PrecargaActaDto(
    EquipoConsolaDto Equipo,
    DatosActaConsola Datos,
    IReadOnlyList<string> AtributosEnMobiControl,
    IReadOnlyList<CampoMobiControlDto> Campos,
    bool PuedeActualizarMobiControl,
    bool CorreoConfigurado);

public class CrearActaConsolaRequest : DatosActaConsola
{
    /// <summary>Se imprime en el acta y se escribe en MobiControl. Sin ella, el día siguiente a la firma.</summary>
    public DateOnly? FechaEntrega { get; set; }

    /// <summary>Mandar el enlace al correo del asociado.</summary>
    public bool EnviarCorreo { get; set; }

    /// <summary>Escribir en MobiControl los atributos corregidos en el formulario.</summary>
    public bool ActualizarMobiControl { get; set; }
}

/// <param name="CorreoEnviado">Null si no se pidió mandarlo.</param>
/// <param name="AtributosActualizados">Atributos que se corrigieron en MobiControl.</param>
/// <param name="ErrorAtributos">Por qué no se pudieron corregir en MobiControl; el enlace se creó igual.</param>
public record ActaConsolaCreadaDto(
    Guid SolicitudUid,
    string UrlFirma,
    DateTime FechaVencimiento,
    bool? CorreoEnviado,
    string? DetalleCorreo,
    IReadOnlyList<string> AtributosActualizados,
    string? ErrorAtributos);

public interface IServicioEquipos
{
    Task<ListaEquiposDto> ListarAsync(CancellationToken ct = default);
    Task<PrecargaActaDto> PrecargaAsync(string deviceId, bool puedeAdministrar, CancellationToken ct = default);

    Task<ActaConsolaCreadaDto> CrearActaAsync(
        string deviceId, CrearActaConsolaRequest solicitud, string? usuario, bool puedeAdministrar, CancellationToken ct = default);
}

/// <summary>
/// Actas desde la consola: se elige un equipo de MobiControl, se precargan sus datos y el asociado
/// firma en esa misma pantalla o desde un enlace. Por debajo es una solicitud de firma como las del
/// sistema de origen: mismo formulario, misma acta, misma marca en MobiControl.
/// </summary>
public class ServicioEquipos(
    IApplicationDbContext db,
    IContextoEmpresa empresa,
    IClienteMobiControl mobiControl,
    IProveedorConfiguracion configuracion,
    IEnlacesFirma enlaces,
    IEnviadorCorreo correo,
    IServicioAtributos atributos,
    ILogger<ServicioEquipos> logger) : IServicioEquipos
{
    private const int VigenciaDias = 7;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Campo del formulario y el atributo de MobiControl del que sale. Son los mismos nombres que
    /// lee el formulario del equipo (<c>%CustomAttr:Cedula%</c>…).
    /// </summary>
    private static readonly (string Campo, string Atributo)[] Atributos =
    [
        ("cedula", "Cedula"),
        ("usuario", "Usuario"),
        ("correo", "Correo"),
        ("numeroCelular", "Numero Celular"),
        ("estado", "Estado"),
        ("canal", "Canal"),
        ("distrito", "Distrito"),
        ("costo", "Costo"),
        ("entregables", "Entregables"),
    ];

    public async Task<ListaEquiposDto> ListarAsync(CancellationToken ct = default)
    {
        var empresaId = EmpresaRequerida();

        var config = await configuracion.ObtenerAsync(empresaId, ct);
        if (config?.MobiControlConfigurado != true)
            return new ListaEquiposDto([], false, DateTimeOffset.UtcNow);

        var equipos = await mobiControl.ListarEquiposAsync(ct);
        var actas = await UltimaActaPorEquipoAsync(ct);
        var enlaces = await EnlacesPendientesAsync(null, ct);

        var lista = equipos
            .Select(e => AVista(e, actas.GetValueOrDefault(e.DeviceId), enlaces.GetValueOrDefault(e.DeviceId)))
            .OrderBy(e => e.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new ListaEquiposDto(lista, true, DateTimeOffset.UtcNow);
    }

    public async Task<PrecargaActaDto> PrecargaAsync(string deviceId, bool puedeAdministrar, CancellationToken ct = default)
    {
        var empresaId = EmpresaRequerida();
        var equipo = await EquipoAsync(deviceId, ct);
        var config = await configuracion.ObtenerAsync(empresaId, ct);

        var (valores, campos) = await AtributosDelEquipoAsync(equipo.DeviceId, ct);

        var datos = new DatosActaConsola
        {
            Cedula = valores.GetValueOrDefault("cedula"),
            Usuario = valores.GetValueOrDefault("usuario"),
            Correo = valores.GetValueOrDefault("correo"),
            Fabricante = TextoMobiControl.Normalizar(equipo.Fabricante, 100),
            Modelo = TextoMobiControl.Normalizar(equipo.Modelo, 100),
            Imei = TextoMobiControl.Normalizar(equipo.Imei, 50),
            Serial = TextoMobiControl.Normalizar(equipo.Serial, 100),
            Iccid = TextoMobiControl.Normalizar(equipo.Iccid, 50),
            // El número de la SIM es lo que reporta el equipo; el atributo manda si lo tiene.
            NumeroCelular = valores.GetValueOrDefault("numeroCelular") ?? TextoMobiControl.Normalizar(equipo.Telefono, 30),
            Estado = valores.GetValueOrDefault("estado"),
            Canal = valores.GetValueOrDefault("canal"),
            Distrito = valores.GetValueOrDefault("distrito"),
            Costo = valores.GetValueOrDefault("costo"),
            Entregables = valores.GetValueOrDefault("entregables"),
            CiudadFirma = config?.CiudadFirma,
        };

        var actas = await UltimaActaPorEquipoAsync(ct, equipo.DeviceId);
        var pendientes = await EnlacesPendientesAsync(equipo.DeviceId, ct);

        return new PrecargaActaDto(
            AVista(equipo, actas.GetValueOrDefault(equipo.DeviceId), pendientes.GetValueOrDefault(equipo.DeviceId)),
            datos,
            [.. campos.Select(c => c.Campo)],
            campos,
            puedeAdministrar,
            config?.CorreoConfigurado ?? false);
    }

    public async Task<ActaConsolaCreadaDto> CrearActaAsync(
        string deviceId, CrearActaConsolaRequest solicitud, string? usuario, bool puedeAdministrar, CancellationToken ct = default)
    {
        var empresaId = EmpresaRequerida();
        var equipo = await EquipoAsync(deviceId, ct);

        var cedula = TextoMobiControl.Normalizar(solicitud.Cedula, 20)
            ?? throw new ErrorSolicitudException("Falta la cédula del asociado.");
        var nombre = TextoMobiControl.Normalizar(solicitud.Usuario, 200)
            ?? throw new ErrorSolicitudException("Falta el nombre del asociado que recibe el equipo.");
        var correoAsociado = TextoMobiControl.Normalizar(solicitud.Correo, 200);

        if (solicitud.EnviarCorreo && correoAsociado is null)
            throw new ErrorSolicitudException("Para mandar el enlace por correo, escribe el correo del asociado.");

        if (correoAsociado is not null && !new EmailAddressAttribute().IsValid(correoAsociado))
            throw new ErrorSolicitudException($"\"{correoAsociado}\" no es un correo válido.");

        if (solicitud.ActualizarMobiControl && !puedeAdministrar)
            throw new ErrorSolicitudException(
                "Solo el dueño o un administrador de la empresa puede cambiar los atributos de MobiControl. Desmarca esa opción y el cambio queda solo en el acta.");

        var config = await configuracion.ObtenerAsync(empresaId, ct);

        // Las listas viajan con el enlace: el formulario del asociado las muestra igual que aquí.
        var listas = (await DefinicionesPorCampoAsync(ct))
            .Where(c => c.Tipo is TiposAtributo.Lista or TiposAtributo.SiNo)
            .ToDictionary(c => c.Campo, c => c.Tipo == TiposAtributo.SiNo ? (IReadOnlyList<string>)["true", "false"] : c.Opciones);

        var imei = TextoMobiControl.Normalizar(solicitud.Imei, 50) ?? TextoMobiControl.Normalizar(equipo.Imei, 50);
        var serial = TextoMobiControl.Normalizar(solicitud.Serial, 100) ?? TextoMobiControl.Normalizar(equipo.Serial, 100);

        var datos = new DatosSolicitud(
            DeviceId: equipo.DeviceId,
            Cedula: cedula,
            Usuario: nombre,
            Correo: correoAsociado,
            Fabricante: TextoMobiControl.Normalizar(solicitud.Fabricante, 100),
            Modelo: TextoMobiControl.Normalizar(solicitud.Modelo, 100),
            Imei: imei,
            Iccid: TextoMobiControl.Normalizar(solicitud.Iccid, 50),
            NumeroCelular: TextoMobiControl.Normalizar(solicitud.NumeroCelular, 30),
            Estado: TextoMobiControl.Normalizar(solicitud.Estado, 50),
            Canal: TextoMobiControl.Normalizar(solicitud.Canal, 100),
            Distrito: TextoMobiControl.Normalizar(solicitud.Distrito, 100),
            Costo: TextoMobiControl.Normalizar(solicitud.Costo, 50),
            Entregables: TextoMobiControl.Normalizar(solicitud.Entregables),
            CiudadFirma: TextoMobiControl.Normalizar(solicitud.CiudadFirma, 100) ?? config?.CiudadFirma,
            TipoDispositivo: TextoMobiControl.Normalizar(solicitud.TipoDispositivo, 30),
            Serial: serial,
            FechaEntrega: solicitud.FechaEntrega,
            NombreDispositivo: TextoMobiControl.Normalizar(equipo.Nombre, 200),
            DispositivoConMobicontrol: true,
            DatosDesdeMobicontrol: true,
            NombreDeInterfaz: "Consola",
            Listas: listas.Count > 0 ? listas : null);

        var ahora = DateTime.UtcNow;

        // Un equipo tiene un solo enlace vivo desde la consola: el nuevo reemplaza al anterior, que
        // vence en este momento. Si no, el asociado podría firmar uno con datos viejos.
        var anteriores = await db.Solicitudes
            .Where(s => s.CreadaEnConsola && s.DeviceId == equipo.DeviceId
                        && s.Estado == EstadoSolicitud.PENDIENTE && s.FechaVencimiento > ahora)
            .ToListAsync(ct);
        foreach (var anterior in anteriores)
        {
            anterior.FechaVencimiento = ahora;
            anterior.FechaActualizacion = ahora;
        }

        var uid = Guid.NewGuid();
        var nueva = new SolicitudFirma
        {
            EmpresaId = empresaId,
            SolicitudUid = uid,
            IdSolicitudOrigen = $"CONSOLA-{uid:N}"[..20].ToUpperInvariant(),
            DatosOrigen = JsonSerializer.Serialize(datos, Json),
            Imei = ServicioSolicitudes.ClaveEquipo(imei, 50),
            Serial = ServicioSolicitudes.ClaveEquipo(serial, 100),
            DeviceId = equipo.DeviceId,
            CreadaEnConsola = true,
            CreadaPor = TextoMobiControl.Normalizar(usuario, 200),
            Estado = EstadoSolicitud.PENDIENTE,
            FechaCreacion = ahora,
            FechaActualizacion = ahora,
            FechaVencimiento = ahora.AddDays(VigenciaDias),
        };

        db.Solicitudes.Add(nueva);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("{Usuario} creó en la consola el enlace de firma {Solicitud} para el equipo {Equipo}.",
            nueva.CreadaPor, nueva.IdSolicitudOrigen, equipo.DeviceId);

        var url = enlaces.UrlParaFirmar(uid);
        var vence = DateTime.SpecifyKind(nueva.FechaVencimiento, DateTimeKind.Utc);

        // Lo que sigue no deshace el enlace: ya existe y se puede firmar. Si falla, se dice cuál
        // parte no salió para que se reintente o se resuelva a mano.
        var (actualizados, errorAtributos) = solicitud.ActualizarMobiControl
            ? await ActualizarAtributosAsync(equipo.DeviceId, solicitud, ct)
            : ([], null);

        bool? enviado = null;
        string? detalle = null;
        if (solicitud.EnviarCorreo)
            (enviado, detalle) = await EnviarEnlaceAsync(config, correoAsociado!, nombre, equipo, url, vence, ct);

        return new ActaConsolaCreadaDto(uid, url, vence, enviado, detalle, actualizados, errorAtributos);
    }

    // ------------------------------------------------------------------------------------

    private int EmpresaRequerida() =>
        empresa.EmpresaId
        ?? throw new ErrorSolicitudException("Elige una empresa para ver sus equipos.");

    private async Task<EquipoMobiControl> EquipoAsync(string deviceId, CancellationToken ct)
    {
        var flota = await mobiControl.ListarEquiposAsync(ct);
        return flota.FirstOrDefault(e => string.Equals(e.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
               ?? throw new ErrorSolicitudException("El equipo no está en la consola de MobiControl de la empresa.");
    }

    /// <summary>
    /// Los valores del equipo para los campos del acta, y el tipo y las opciones de cada campo que
    /// tiene atributo en la consola. Un valor heredado del grupo también sirve: es el que el
    /// formulario del equipo vería.
    /// </summary>
    private async Task<(Dictionary<string, string> Valores, IReadOnlyList<CampoMobiControlDto> Campos)> AtributosDelEquipoAsync(
        string deviceId, CancellationToken ct)
    {
        var lista = await mobiControl.ValoresAtributosAsync(deviceId, ct);
        var campos = await DefinicionesPorCampoAsync(ct);
        var valores = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var campo in campos)
        {
            if (Buscar(lista, campo.Atributo) is not { } valor) continue;
            if (TextoMobiControl.Normalizar(valor.Valor) is not { } texto) continue;

            valores[campo.Campo] = campo.Tipo == TiposAtributo.Numero ? NumeroLegible(texto) : texto;
        }

        return (valores, campos);
    }

    /// <summary>Los campos del acta que tienen atributo en la consola, con su tipo y opciones.</summary>
    private async Task<IReadOnlyList<CampoMobiControlDto>> DefinicionesPorCampoAsync(CancellationToken ct)
    {
        var definiciones = await mobiControl.ListarAtributosAsync(ct);
        var campos = new List<CampoMobiControlDto>();

        foreach (var (campo, atributo) in Atributos)
        {
            var d = definiciones.FirstOrDefault(x => x.Nombre == atributo)
                    ?? definiciones.FirstOrDefault(x => string.Equals(x.Nombre, atributo, StringComparison.OrdinalIgnoreCase));
            if (d is not null)
                campos.Add(new CampoMobiControlDto(campo, d.Nombre, d.Tipo, d.Opciones));
        }

        return campos;
    }

    /// <summary>
    /// MobiControl guarda los números como decimales: un celular puede llegar "3001234567.0" o en
    /// notación científica. Se muestran como se escriben.
    /// </summary>
    private static string NumeroLegible(string valor) =>
        decimal.TryParse(valor, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            : valor;

    private static ValorAtributoMobiControl? Buscar(IReadOnlyList<ValorAtributoMobiControl> lista, string atributo) =>
        lista.FirstOrDefault(v => v.Nombre == atributo)
        ?? lista.FirstOrDefault(v => string.Equals(v.Nombre, atributo, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Escribe en MobiControl los atributos que se corrigieron en el formulario. Solo los que
    /// cambiaron y existen en la consola: un atributo que no está no se crea por esta vía.
    /// </summary>
    private async Task<(IReadOnlyList<string>, string?)> ActualizarAtributosAsync(
        string deviceId, CrearActaConsolaRequest solicitud, CancellationToken ct)
    {
        try
        {
            var lista = await mobiControl.ValoresAtributosAsync(deviceId, ct);
            var nuevos = new Dictionary<string, string?>
            {
                ["cedula"] = solicitud.Cedula, ["usuario"] = solicitud.Usuario, ["correo"] = solicitud.Correo,
                ["numeroCelular"] = solicitud.NumeroCelular, ["estado"] = solicitud.Estado, ["canal"] = solicitud.Canal,
                ["distrito"] = solicitud.Distrito, ["costo"] = solicitud.Costo, ["entregables"] = solicitud.Entregables,
            };

            var cambios = new List<CambioValorAtributo>();
            foreach (var definicion in await DefinicionesPorCampoAsync(ct))
            {
                if (Buscar(lista, definicion.Atributo) is not { } actual) continue;

                var nuevo = TextoMobiControl.Normalizar(nuevos[definicion.Campo]);
                if (nuevo is null) continue;

                // Los números se comparan como números: "3001234567" y "3001234567.0" son el mismo
                // valor, y reescribirlo sería un cambio que nadie hizo.
                var anterior = TextoMobiControl.Normalizar(actual.Valor);
                if (definicion.Tipo == TiposAtributo.Numero && anterior is not null)
                    (nuevo, anterior) = (NumeroLegible(nuevo), NumeroLegible(anterior));
                if (nuevo == anterior) continue;

                cambios.Add(new CambioValorAtributo { Nombre = actual.Nombre, Valor = nuevo });
            }

            if (cambios.Count == 0) return ([], null);

            await atributos.GuardarValoresAsync(deviceId, new GuardarValoresRequest { Valores = cambios }, ct);
            return ([.. cambios.Select(c => c.Nombre)], null);
        }
        catch (ErrorSolicitudException ex)
        {
            logger.LogWarning("No se pudieron corregir los atributos de {Equipo} en MobiControl: {Motivo}", deviceId, ex.Message);
            return ([], ex.Message);
        }
    }

    private async Task<(bool, string?)> EnviarEnlaceAsync(
        ConfiguracionEmpresa? config, string destino, string nombre, EquipoMobiControl equipo,
        string url, DateTime vence, CancellationToken ct)
    {
        if (config?.CorreoConfigurado != true)
            return (false, "La empresa no tiene configurado el envío de correos en One. Copia el enlace y envíalo por otro medio.");

        static string e(string? texto) => WebUtility.HtmlEncode(texto) ?? "";
        var modelo = string.Join(" ", new[] { equipo.Fabricante, equipo.Modelo }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var hasta = TimeZoneInfo.ConvertTimeFromUtc(vence, ZonaColombia).ToString("dd/MM/yyyy");

        var html = $"""
            <p>Hola {e(nombre)},</p>
            <p>{e(config.TenantNombre)} te entrega {(modelo.Length > 0 ? $"el equipo <b>{e(modelo)}</b>" : "un equipo")}. Revisa los datos del acta y fírmala desde este enlace:</p>
            <p><a href="{e(url)}" style="display:inline-block;padding:10px 18px;background:#af1839;color:#ffffff;text-decoration:none;border-radius:6px;font-family:sans-serif">Revisar y firmar el acta</a></p>
            <p style="font-family:sans-serif;font-size:13px;color:#5b6a80">Si el botón no abre, copia esta dirección en el navegador:<br>{e(url)}</p>
            <p style="font-family:sans-serif;font-size:13px;color:#5b6a80">El enlace sirve hasta el {hasta}. Si los datos no corresponden, puedes rechazar el acta desde el mismo enlace.</p>
            <p style="color:#5b6a80;font-size:12px">{e(config.TenantNombre)} · Mensaje automático, no responder.</p>
            """;

        var resultado = await correo.EnviarAsync(config, [destino], $"Acta de entrega para firmar · {config.TenantNombre}", html, ct);
        return resultado.Exitoso
            ? (true, null)
            : (false, $"No se pudo enviar el correo: {resultado.Detalle}");
    }

    private static readonly TimeZoneInfo ZonaColombia = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    /// <summary>La última acta de cada equipo (o de uno solo), por DeviceId.</summary>
    private async Task<Dictionary<string, ActaEquipoDto>> UltimaActaPorEquipoAsync(CancellationToken ct, string? deviceId = null)
    {
        var consulta = db.Entregas.AsNoTracking().Where(e => e.Dispositivo.MobiControlDeviceId != null);
        if (deviceId is not null)
            consulta = consulta.Where(e => e.Dispositivo.MobiControlDeviceId == deviceId);

        var filas = await consulta
            .OrderByDescending(e => e.FechaFirma)
            .Select(e => new
            {
                DeviceId = e.Dispositivo.MobiControlDeviceId!,
                e.EntregaUid,
                e.NombreAsociadoFirmante,
                e.Empleado.Cedula,
                e.FechaFirma,
            })
            .ToListAsync(ct);

        var porEquipo = new Dictionary<string, ActaEquipoDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in filas)
            porEquipo.TryAdd(f.DeviceId, new ActaEquipoDto(
                f.EntregaUid, f.NombreAsociadoFirmante, f.Cedula, DateTime.SpecifyKind(f.FechaFirma, DateTimeKind.Utc)));

        return porEquipo;
    }

    /// <summary>Enlaces de firma vigentes por equipo; si hay varios, el más reciente.</summary>
    private async Task<Dictionary<string, EnlacePendienteDto>> EnlacesPendientesAsync(string? deviceId, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var consulta = db.Solicitudes.AsNoTracking()
            .Where(s => s.DeviceId != null && s.Estado == EstadoSolicitud.PENDIENTE && s.FechaVencimiento > ahora);
        if (deviceId is not null)
            consulta = consulta.Where(s => s.DeviceId == deviceId);

        var filas = await consulta
            .OrderByDescending(s => s.FechaCreacion)
            .Select(s => new { DeviceId = s.DeviceId!, s.SolicitudUid, s.FechaVencimiento, s.DatosOrigen, s.CreadaPor })
            .ToListAsync(ct);

        var porEquipo = new Dictionary<string, EnlacePendienteDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in filas)
        {
            if (porEquipo.ContainsKey(f.DeviceId)) continue;

            var correoDestino = JsonSerializer.Deserialize<DatosSolicitud>(f.DatosOrigen, Json)?.Correo;
            porEquipo[f.DeviceId] = new EnlacePendienteDto(
                f.SolicitudUid, enlaces.UrlParaFirmar(f.SolicitudUid),
                DateTime.SpecifyKind(f.FechaVencimiento, DateTimeKind.Utc), correoDestino, f.CreadaPor);
        }

        return porEquipo;
    }

    private static EquipoConsolaDto AVista(EquipoMobiControl e, ActaEquipoDto? acta, EnlacePendienteDto? enlace) =>
        new(e.DeviceId, e.Nombre, e.Plataforma, e.Fabricante, e.Modelo, e.Imei, e.Serial, e.Telefono, e.Grupo,
            e.EnLinea, e.UltimoReporte, acta, enlace);
}
