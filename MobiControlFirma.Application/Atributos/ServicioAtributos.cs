using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MobiControlFirma.Application.Common;
using MobiControlFirma.Application.Common.Interfaces;

namespace MobiControlFirma.Application.Atributos;

/// <param name="Tipo">Como lo nombra MobiControl: Text, Enumerator, Date, Float, Boolean.</param>
/// <param name="UsoEnFirma">Para qué lo usa firma, o null si firma no lo usa. Los que usa no se editan ni se borran.</param>
public record AtributoDto(
    string Nombre, string Tipo, IReadOnlyList<string> Opciones, bool PasaAlEquipo, string? ReferenceId, string? UsoEnFirma);

/// <param name="AtributoFirma">Atributo que firma marca al firmar un acta.</param>
/// <param name="AtributoFecha">Atributo donde firma deja la fecha de la entrega.</param>
/// <param name="ElegidosEnFirma">True si se eligieron en esta consola; false si vienen de One o son los de siempre.</param>
/// <param name="PuedeAdministrar">Dueño o administrador de la empresa, o de plataforma: puede cambiar la consola.</param>
public record ListaAtributosDto(
    IReadOnlyList<AtributoDto> Atributos, string AtributoFirma, string AtributoFecha, bool ElegidosEnFirma, bool PuedeAdministrar);

/// <param name="Valor">El del equipo. El de ApiKey va enmascarado: es la llave del dispositivo.</param>
/// <param name="Heredado">El equipo no tiene valor propio: lo toma de su grupo en MobiControl.</param>
/// <param name="Origen">Grupo del que lo hereda.</param>
/// <param name="Editable">Se puede cambiar desde aquí (ApiKey no).</param>
public record ValorAtributoDto(
    string Nombre, string Tipo, IReadOnlyList<string> Opciones, string? Valor, bool Heredado, string? Origen, bool Editable, string? UsoEnFirma);

/// <param name="PuedeAdministrar">Puede cambiar los valores (dueño o administrador de la empresa, o de plataforma).</param>
public record ValoresEquipoDto(IReadOnlyList<ValorAtributoDto> Valores, bool PuedeAdministrar);

public class GuardarAtributoRequest
{
    [Required, MaxLength(100)] public string Nombre { get; set; } = string.Empty;
    [Required] public string Tipo { get; set; } = TiposAtributo.Texto;
    /// <summary>Solo para las listas (Enumerator).</summary>
    public List<string>? Opciones { get; set; }
    /// <summary>PropagateToDevice: el valor se copia al equipo y sus apps lo pueden leer.</summary>
    public bool PasaAlEquipo { get; set; } = true;
}

public class ElegirAtributosRequest
{
    [Required, MaxLength(100)] public string AtributoFirma { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string AtributoFecha { get; set; } = string.Empty;
}

public class CambioValorAtributo
{
    [Required, MaxLength(100)] public string Nombre { get; set; } = string.Empty;
    [MaxLength(500)] public string Valor { get; set; } = string.Empty;
}

public class GuardarValoresRequest
{
    [Required] public List<CambioValorAtributo> Valores { get; set; } = [];
}

public interface IServicioAtributos
{
    Task<ListaAtributosDto> ListarAsync(bool puedeAdministrar, CancellationToken ct = default);
    Task<AtributoDto> CrearAsync(GuardarAtributoRequest solicitud, CancellationToken ct = default);
    Task<AtributoDto> ActualizarAsync(string nombreActual, GuardarAtributoRequest solicitud, CancellationToken ct = default);
    Task EliminarAsync(string nombre, CancellationToken ct = default);

    /// <summary>Elige los atributos que firma escribe al firmar. Mandan sobre las variables de One.</summary>
    Task ElegirAsync(ElegirAtributosRequest solicitud, CancellationToken ct = default);

    Task<ValoresEquipoDto> ValoresAsync(string deviceId, bool puedeAdministrar, CancellationToken ct = default);
    Task<ValoresEquipoDto> GuardarValoresAsync(string deviceId, GuardarValoresRequest solicitud, CancellationToken ct = default);
}

/// <summary>
/// Los atributos personalizados de la consola de MobiControl de la empresa. MobiControl es la
/// fuente: aquí no se guarda ninguno, salvo la elección de cuáles escribe firma al firmar.
///
/// Firma depende de tres: el que marca el equipo como firmado, el de la fecha de entrega y
/// "ApiKey", que firma_fe lee en el equipo para saber de qué empresa es. Esos no se editan ni se
/// borran desde aquí: hacerlo dejaría de registrar o sincronizar actas.
/// </summary>
public class ServicioAtributos(
    IClienteMobiControl mobiControl,
    IProveedorConfiguracion configuracion,
    IApplicationDbContext db,
    IContextoEmpresa empresa) : IServicioAtributos
{
    private int EmpresaId => empresa.EmpresaId
        ?? throw new ErrorSolicitudException("Elige una empresa para ver los atributos de su consola de MobiControl.");

    /// <summary>Lo lee firma_fe en el equipo con %CustomAttr:ApiKey%.</summary>
    public const string AtributoLlave = "ApiKey";

    public async Task<ListaAtributosDto> ListarAsync(bool puedeAdministrar, CancellationToken ct = default)
    {
        var (firma, fecha, elegidos) = await EnUsoAsync(ct);
        var atributos = await mobiControl.ListarAtributosAsync(ct);

        return new ListaAtributosDto(
            [.. atributos.OrderBy(a => a.Nombre, StringComparer.CurrentCultureIgnoreCase).Select(a => AVista(a, firma, fecha))],
            firma, fecha, elegidos, puedeAdministrar);
    }

    public async Task<AtributoDto> CrearAsync(GuardarAtributoRequest solicitud, CancellationToken ct = default)
    {
        var atributo = Validar(solicitud);
        var creado = await mobiControl.CrearAtributoAsync(atributo, ct);
        var (firma, fecha, _) = await EnUsoAsync(ct);
        return AVista(creado, firma, fecha);
    }

    public async Task<AtributoDto> ActualizarAsync(string nombreActual, GuardarAtributoRequest solicitud, CancellationToken ct = default)
    {
        var (firma, fecha, _) = await EnUsoAsync(ct);
        if (Uso(nombreActual, firma, fecha) is { } uso)
            throw new ErrorSolicitudException($"\"{nombreActual}\" lo usa firma ({uso}): no se puede cambiar desde aquí.");

        var atributo = Validar(solicitud);
        if (!string.Equals(nombreActual, atributo.Nombre, StringComparison.Ordinal) && Uso(atributo.Nombre, firma, fecha) is not null)
            throw new ErrorSolicitudException($"\"{atributo.Nombre}\" es un nombre que usa firma.");

        var actualizado = await mobiControl.ActualizarAtributoAsync(nombreActual, atributo, ct);
        return AVista(actualizado, firma, fecha);
    }

    public async Task EliminarAsync(string nombre, CancellationToken ct = default)
    {
        var (firma, fecha, _) = await EnUsoAsync(ct);
        if (Uso(nombre, firma, fecha) is { } uso)
            throw new ErrorSolicitudException($"\"{nombre}\" lo usa firma ({uso}): no se puede eliminar.");

        if (!await mobiControl.EliminarAtributoAsync(nombre, ct))
            throw new ErrorSolicitudException($"MobiControl ya no tiene un atributo llamado \"{nombre}\".");
    }

    public async Task ElegirAsync(ElegirAtributosRequest solicitud, CancellationToken ct = default)
    {
        var atributos = await mobiControl.ListarAtributosAsync(ct);

        var firma = Buscar(atributos, solicitud.AtributoFirma)
            ?? throw new ErrorSolicitudException($"MobiControl no tiene un atributo llamado \"{solicitud.AtributoFirma}\".");
        var fecha = Buscar(atributos, solicitud.AtributoFecha)
            ?? throw new ErrorSolicitudException($"MobiControl no tiene un atributo llamado \"{solicitud.AtributoFecha}\".");

        // Firma escribe true y una fecha: en un atributo de otro tipo MobiControl rechazaría cada acta.
        if (firma.Tipo != TiposAtributo.SiNo)
            throw new ErrorSolicitudException($"El atributo de la firma debe ser de tipo sí/no; \"{firma.Nombre}\" no lo es.");
        if (fecha.Tipo != TiposAtributo.Fecha)
            throw new ErrorSolicitudException($"El atributo de la fecha debe ser de tipo fecha; \"{fecha.Nombre}\" no lo es.");
        if (firma.Nombre == fecha.Nombre)
            throw new ErrorSolicitudException("La firma y la fecha deben ir en atributos distintos.");

        var fila = await db.Empresas.FirstAsync(e => e.EmpresaId == EmpresaId, ct);
        fila.AtributoFirma = firma.Nombre;
        fila.AtributoFecha = fecha.Nombre;
        fila.FechaActualizacion = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // La configuración se cachea unos minutos: la próxima acta tiene que usar la elección nueva.
        configuracion.Olvidar(fila.EmpresaId);
    }

    public async Task<ValoresEquipoDto> ValoresAsync(string deviceId, bool puedeAdministrar, CancellationToken ct = default)
    {
        var (firma, fecha, _) = await EnUsoAsync(ct);
        var definiciones = (await mobiControl.ListarAtributosAsync(ct)).ToDictionary(a => a.Nombre, StringComparer.Ordinal);
        var valores = await mobiControl.ValoresAtributosAsync(deviceId, ct);

        return new ValoresEquipoDto(
        [
            .. valores
                .Select(v =>
                {
                    definiciones.TryGetValue(v.Nombre, out var d);
                    var esLlave = v.Nombre == AtributoLlave;
                    return new ValorAtributoDto(
                        v.Nombre,
                        d?.Tipo ?? TiposAtributo.Texto,
                        d?.Opciones ?? [],
                        esLlave ? Enmascarar(v.Valor) : v.Valor,
                        v.Heredado,
                        v.Origen,
                        Editable: !esLlave && d is not null,
                        Uso(v.Nombre, firma, fecha));
                })
                .OrderBy(v => v.Nombre, StringComparer.CurrentCultureIgnoreCase)
        ], puedeAdministrar);
    }

    public async Task<ValoresEquipoDto> GuardarValoresAsync(
        string deviceId, GuardarValoresRequest solicitud, CancellationToken ct = default)
    {
        if (solicitud.Valores.Count == 0) throw new ErrorSolicitudException("No hay cambios para guardar.");

        var definiciones = (await mobiControl.ListarAtributosAsync(ct)).ToDictionary(a => a.Nombre, StringComparer.Ordinal);
        var cambios = new List<(string Nombre, object Valor)>();

        foreach (var cambio in solicitud.Valores)
        {
            var nombre = cambio.Nombre.Trim();
            if (nombre == AtributoLlave)
                throw new ErrorSolicitudException("La llave del equipo (ApiKey) no se cambia desde aquí.");
            if (!definiciones.TryGetValue(nombre, out var definicion))
                throw new ErrorSolicitudException($"MobiControl no tiene un atributo llamado \"{nombre}\".");

            cambios.Add((nombre, ValorValido(definicion, cambio.Valor)));
        }

        await mobiControl.GuardarValoresAtributosAsync(deviceId, cambios, ct);
        return await ValoresAsync(deviceId, true, ct);
    }

    // ------------------------------------------------------------------------------------

    /// <summary>Los atributos que escribe firma, ya resueltos: los elegidos aquí, o los de One.</summary>
    private async Task<(string Firma, string Fecha, bool Elegidos)> EnUsoAsync(CancellationToken ct)
    {
        var fila = await db.Empresas.AsNoTracking()
            .Where(e => e.EmpresaId == EmpresaId)
            .Select(e => new { e.AtributoFirma, e.AtributoFecha })
            .FirstAsync(ct);

        var config = await configuracion.ObtenerAsync(EmpresaId, ct);
        return (
            config?.MobiControlAtributoFirma ?? fila.AtributoFirma ?? "Firma de entrega",
            config?.MobiControlAtributoFecha ?? fila.AtributoFecha ?? "Fecha de entrega",
            fila.AtributoFirma is not null);
    }

    private static string? Uso(string nombre, string firma, string fecha) =>
        nombre == firma ? "marca el equipo como firmado"
        : nombre == fecha ? "guarda la fecha de entrega"
        : nombre == AtributoLlave ? "firma_fe lee la llave del equipo"
        : null;

    private static AtributoDto AVista(AtributoMobiControl a, string firma, string fecha) =>
        new(a.Nombre, a.Tipo, a.Opciones, a.PasaAlEquipo, a.ReferenceId, Uso(a.Nombre, firma, fecha));

    private static AtributoMobiControl? Buscar(IReadOnlyList<AtributoMobiControl> atributos, string nombre) =>
        atributos.FirstOrDefault(a => a.Nombre == nombre.Trim());

    private static AtributoMobiControl Validar(GuardarAtributoRequest s)
    {
        var nombre = s.Nombre?.Trim();
        if (string.IsNullOrEmpty(nombre)) throw new ErrorSolicitudException("El atributo necesita un nombre.");

        // El nombre viaja en la ruta de MobiControl (/customattributes/{nombre}) y en las macros
        // del equipo (%CustomAttr:nombre%).
        if (nombre.IndexOfAny(['/', '\\', '?', '#', '%', ':']) >= 0)
            throw new ErrorSolicitudException("El nombre no puede llevar / \\ ? # % ni :.");

        if (!TiposAtributo.Todos.Contains(s.Tipo))
            throw new ErrorSolicitudException("Tipo de atributo desconocido.");

        var opciones = s.Tipo == TiposAtributo.Lista
            ? (s.Opciones ?? []).Select(o => o.Trim()).Where(o => o.Length > 0).Distinct(StringComparer.Ordinal).ToList()
            : [];

        if (s.Tipo == TiposAtributo.Lista && opciones.Count == 0)
            throw new ErrorSolicitudException("Una lista necesita al menos una opción.");
        if (opciones.Any(o => o.Length > 100))
            throw new ErrorSolicitudException("Cada opción puede tener hasta 100 caracteres.");

        return new AtributoMobiControl(nombre, s.Tipo, opciones, s.PasaAlEquipo);
    }

    /// <summary>El valor con el tipo JSON que MobiControl espera para el atributo.</summary>
    private static object ValorValido(AtributoMobiControl d, string? valor)
    {
        var v = (valor ?? string.Empty).Trim();

        switch (d.Tipo)
        {
            case TiposAtributo.SiNo:
                return v.ToLowerInvariant() switch
                {
                    "true" or "si" or "sí" => true,
                    "false" or "no" => false,
                    _ => throw new ErrorSolicitudException($"\"{d.Nombre}\" es sí/no."),
                };

            case TiposAtributo.Fecha:
                return DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
                    ? f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : throw new ErrorSolicitudException($"\"{d.Nombre}\" es una fecha (AAAA-MM-DD).");

            case TiposAtributo.Numero:
                return decimal.TryParse(v.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
                    ? n
                    : throw new ErrorSolicitudException($"\"{d.Nombre}\" es un número.");

            case TiposAtributo.Lista:
                return d.Opciones.Contains(v)
                    ? v
                    : throw new ErrorSolicitudException($"\"{v}\" no es una opción de \"{d.Nombre}\".");

            default:
                return v.Length <= 500 ? v : throw new ErrorSolicitudException($"\"{d.Nombre}\" admite hasta 500 caracteres.");
        }
    }

    private static string? Enmascarar(string? llave) =>
        string.IsNullOrEmpty(llave) ? llave : llave.Length <= 4 ? "••••" : $"••••{llave[^4..]}";
}
