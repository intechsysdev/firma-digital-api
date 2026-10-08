using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MobiControlFirma.Application.Common;
using MobiControlFirma.Application.Common.Interfaces;

namespace MobiControlFirma.Infrastructure.MobiControl;

/// <summary>
/// Atributos personalizados de la consola: sus definiciones y sus valores en cada equipo. Lo que
/// se sabe de la API (comprobado contra la consola):
///
/// - <c>GET /customattributes</c> los lista todos; <c>GET /customattributes/{nombre}</c> trae uno
///   (403 si no existe, no 404).
/// - <c>POST /customattributes</c> crea; <c>PUT /customattributes/{nombre}</c> cambia nombre, tipo,
///   opciones y si se pasa al equipo (conserva el ReferenceId); <c>DELETE</c> lo borra.
/// - Hay que mandar todos los campos: con uno de menos responde "Invalid JSON content".
/// - <c>GET/PUT /devices/{id}/customAttributes</c>: los valores de un equipo. Los heredados del
///   grupo vienen con <c>IsInherited</c> y sin valor.
/// </summary>
public partial class ClienteMobiControl
{
    public async Task<IReadOnlyList<AtributoMobiControl>> ListarAtributosAsync(CancellationToken ct = default)
    {
        var (config, empresaId, token) = await SesionAsync(ct);
        var (codigo, texto) = await AtributoAsync(empresaId, config, token, HttpMethod.Get, "api/customattributes", null, ct);
        Asegurar(codigo, texto, null);

        using var json = JsonDocument.Parse(texto);
        if (json.RootElement.ValueKind != JsonValueKind.Array) return [];
        return [.. json.RootElement.EnumerateArray().Select(LeerAtributo).Where(a => a.Nombre.Length > 0)];
    }

    public async Task<AtributoMobiControl> CrearAtributoAsync(AtributoMobiControl atributo, CancellationToken ct = default)
    {
        var (config, empresaId, token) = await SesionAsync(ct);
        var (codigo, texto) = await AtributoAsync(empresaId, config, token, HttpMethod.Post, "api/customattributes", Cuerpo(atributo), ct);
        Asegurar(codigo, texto, atributo.Nombre);
        using var json = JsonDocument.Parse(texto);
        return LeerAtributo(json.RootElement);
    }

    public async Task<AtributoMobiControl> ActualizarAtributoAsync(
        string nombreActual, AtributoMobiControl atributo, CancellationToken ct = default)
    {
        var (config, empresaId, token) = await SesionAsync(ct);
        var ruta = $"api/customattributes/{Uri.EscapeDataString(nombreActual)}";

        var (codigo, texto) = await AtributoAsync(empresaId, config, token, HttpMethod.Put, ruta, Cuerpo(atributo), ct);
        if (codigo is 403 or 404)
            throw new ErrorSolicitudException($"El atributo \"{nombreActual}\" ya no está en MobiControl.");
        Asegurar(codigo, texto, atributo.Nombre);

        // PUT responde 204 sin cuerpo: se lee como quedó.
        var (final, cuerpo) = await AtributoAsync(empresaId, config, token, HttpMethod.Get,
            $"api/customattributes/{Uri.EscapeDataString(atributo.Nombre)}", null, ct);
        Asegurar(final, cuerpo, atributo.Nombre);
        using var json = JsonDocument.Parse(cuerpo);
        return LeerAtributo(json.RootElement);
    }

    public async Task<bool> EliminarAtributoAsync(string nombre, CancellationToken ct = default)
    {
        var (config, empresaId, token) = await SesionAsync(ct);
        var (codigo, texto) = await AtributoAsync(empresaId, config, token, HttpMethod.Delete,
            $"api/customattributes/{Uri.EscapeDataString(nombre)}", null, ct);

        if (codigo is 403 or 404) return false;
        Asegurar(codigo, texto, nombre);
        return true;
    }

    public async Task<IReadOnlyList<ValorAtributoMobiControl>> ValoresAtributosAsync(string deviceId, CancellationToken ct = default)
    {
        var (config, empresaId, token) = await SesionAsync(ct);
        var (codigo, texto) = await AtributoAsync(empresaId, config, token, HttpMethod.Get,
            $"api/devices/{Uri.EscapeDataString(deviceId)}/customAttributes", null, ct);

        if (codigo is 403 or 404)
            throw new ErrorSolicitudException("MobiControl no tiene ese equipo.");
        Asegurar(codigo, texto, null);

        using var json = JsonDocument.Parse(texto);
        if (json.RootElement.ValueKind != JsonValueKind.Array) return [];

        return
        [
            .. json.RootElement.EnumerateArray()
                .Select(v => new ValorAtributoMobiControl(
                    Texto(v, "Name") ?? string.Empty,
                    Valor(Texto(v, "Value"), Texto(v, "DataType")),
                    Logico(v, "IsInherited") ?? false,
                    Texto(v, "OriginName") is { Length: > 0 } origen ? origen : null))
                .Where(v => v.Nombre.Length > 0)
        ];
    }

    public async Task GuardarValoresAtributosAsync(
        string deviceId, IReadOnlyList<(string Nombre, object Valor)> valores, CancellationToken ct = default)
    {
        if (valores.Count == 0) return;

        var (config, empresaId, token) = await SesionAsync(ct);
        var cuerpo = new
        {
            // Como al firmar un acta: cada valor con su tipo JSON (true y no "true").
            Attributes = valores.Select(v => new { AttributeName = v.Nombre, AttributeValue = v.Valor }).ToArray(),
        };

        var (codigo, texto) = await AtributoAsync(empresaId, config, token, HttpMethod.Put,
            $"api/devices/{Uri.EscapeDataString(deviceId)}/customAttributes", cuerpo, ct);

        if (codigo is 403 or 404)
            throw new ErrorSolicitudException("MobiControl no tiene ese equipo.");
        Asegurar(codigo, texto, null);
    }

    // ------------------------------------------------------------------------------------

    private async Task<(int Codigo, string Texto)> AtributoAsync(
        int empresaId, ConfiguracionEmpresa config, string token, HttpMethod metodo, string ruta, object? cuerpo, CancellationToken ct)
    {
        using var limite = ConLimite(config, ct);
        using var peticion = new HttpRequestMessage(metodo, Ruta(config, ruta));
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        peticion.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (cuerpo is not null)
            peticion.Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json");

        try
        {
            using var respuesta = await http.SendAsync(peticion, limite.Token);
            var codigo = (int)respuesta.StatusCode;
            if (codigo == 401) Tokens.TryRemove(empresaId, out _);
            return (codigo, await respuesta.Content.ReadAsStringAsync(ct));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ErrorSolicitudException("MobiControl no respondió a tiempo. Intente de nuevo en un momento.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "No se pudo conectar con MobiControl de {Empresa}.", config.TenantNombre);
            throw new ErrorSolicitudException("No se pudo conectar con la consola de MobiControl.");
        }
    }

    /// <summary>Todos los campos: MobiControl rechaza el cuerpo si falta alguno.</summary>
    private static object Cuerpo(AtributoMobiControl a) => new
    {
        Name = a.Nombre,
        CustomAttributeDataType = a.Tipo,
        EnumValues = a.Tipo == TiposAtributo.Lista ? a.Opciones : [],
        PropagateToDevice = a.PasaAlEquipo,
    };

    private static AtributoMobiControl LeerAtributo(JsonElement a)
    {
        var opciones = a.TryGetProperty("EnumValues", out var lista) && lista.ValueKind == JsonValueKind.Array
            ? lista.EnumerateArray().Select(o => o.GetString()).OfType<string>().ToList()
            : [];

        return new AtributoMobiControl(
            Texto(a, "Name") ?? string.Empty,
            Texto(a, "CustomAttributeDataType") ?? TiposAtributo.Texto,
            opciones,
            Logico(a, "PropagateToDevice") ?? false,
            Texto(a, "ReferenceId"));
    }

    /// <summary>
    /// Las fechas llegan en el formato viejo de WCF, "/Date(1789171200000)/" (milisegundos UTC):
    /// se pasan a AAAA-MM-DD, que es como se muestran y como se escriben. Los sí/no llegan como
    /// "True"/"False": se dejan en minúscula, igual que se escriben.
    /// </summary>
    private static string? Valor(string? valor, string? tipo)
    {
        if (valor is null) return null;

        if (tipo == TiposAtributo.Fecha)
        {
            var wcf = System.Text.RegularExpressions.Regex.Match(valor, @"^/Date\((-?\d+)([+-]\d{4})?\)/$");
            if (wcf.Success && long.TryParse(wcf.Groups[1].Value, out var ms))
                return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString("yyyy-MM-dd");
        }

        return tipo == TiposAtributo.SiNo ? valor.ToLowerInvariant() : valor;
    }

    /// <summary>Traduce los rechazos de MobiControl a algo que se pueda mostrar.</summary>
    private static void Asegurar(int codigo, string texto, string? nombre)
    {
        if (codigo is >= 200 and < 300) return;

        string? mensaje = null;
        try
        {
            using var json = JsonDocument.Parse(texto);
            mensaje = Texto(json.RootElement, "Message");
        }
        catch (JsonException)
        {
            // Sin cuerpo legible: queda el código.
        }

        if (mensaje?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true)
            throw new ErrorSolicitudException($"Ya existe en MobiControl un atributo llamado \"{nombre}\".");

        if (codigo == 400 && mensaje == "Contract validation failed")
            throw new ErrorSolicitudException("MobiControl no aceptó el atributo. Revise el nombre, el tipo y las opciones.");

        throw new ErrorSolicitudException(
            $"MobiControl rechazó la operación ({codigo}){(string.IsNullOrWhiteSpace(mensaje) ? "." : $": {mensaje}")}");
    }
}
