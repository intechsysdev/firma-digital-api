using System.Globalization;
using System.Text.RegularExpressions;

namespace MobiControlFirma.Application.Common;

/// <summary>
/// Limpieza de los valores que llegan del formulario instalado en el dispositivo.
/// </summary>
public static partial class TextoMobiControl
{
    /// <summary>
    /// Un marcador que MobiControl no alcanzó a resolver llega tal cual: <c>%CustomAttr:Costo%</c>.
    /// Si eso se guardara, la base terminaría con actas que dicen "%MODEL%" como modelo, así que
    /// se descarta y la columna queda nula.
    /// </summary>
    [GeneratedRegex(@"^%[^%]*%$")]
    private static partial Regex MarcadorSinResolver();

    /// <summary>Texto utilizable, o null si venía vacío o sin resolver.</summary>
    public static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;

        var limpio = valor.Trim();
        if (MarcadorSinResolver().IsMatch(limpio)) return null;

        // Valores que MobiControl envía cuando el atributo existe pero está vacío.
        if (limpio is "N/A" or "n/a" or "null" or "-") return null;

        return limpio;
    }

    /// <summary>Igual que <see cref="Normalizar"/>, recortando a lo que acepta la columna.</summary>
    public static string? Normalizar(string? valor, int maximo)
    {
        var limpio = Normalizar(valor);
        return limpio is null || limpio.Length <= maximo ? limpio : limpio[..maximo];
    }

    /// <summary>
    /// Interpreta el costo escrito para humanos. Llega de todo: "$ 1.200.000", "1,200,000.00",
    /// "1200000". Se resuelve por la posición del último separador en vez de asumir una cultura.
    /// </summary>
    public static decimal? ParsearMoneda(string? valor)
    {
        var limpio = Normalizar(valor);
        if (limpio is null) return null;

        limpio = Regex.Replace(limpio, @"[^\d.,\-]", string.Empty);
        if (limpio.Length == 0) return null;

        var ultimaComa = limpio.LastIndexOf(',');
        var ultimoPunto = limpio.LastIndexOf('.');
        var separadorDecimal = Math.Max(ultimaComa, ultimoPunto);

        // Con dos decimales o menos después del último separador, ese separador es el decimal;
        // con tres es un separador de miles ("1.200" son mil doscientos pesos, no 1,2).
        var esDecimal = separadorDecimal >= 0 && limpio.Length - separadorDecimal - 1 is > 0 and <= 2;

        string normalizado;
        if (esDecimal)
        {
            var entero = Regex.Replace(limpio[..separadorDecimal], @"[.,]", string.Empty);
            normalizado = $"{entero}.{limpio[(separadorDecimal + 1)..]}";
        }
        else
        {
            normalizado = Regex.Replace(limpio, @"[.,]", string.Empty);
        }

        return decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out var resultado)
            ? resultado
            : null;
    }

    /// <summary>
    /// Convierte la firma en bytes. El canvas la manda como data URL; se acepta también el
    /// base64 pelado por si el cliente cambia.
    /// </summary>
    public static byte[]? DecodificarImagenBase64(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;

        var datos = valor.Trim();
        var coma = datos.IndexOf(',');
        if (datos.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && coma > 0)
            datos = datos[(coma + 1)..];

        datos = Regex.Replace(datos, @"\s", string.Empty);

        try
        {
            var bytes = Convert.FromBase64String(datos);
            return bytes.Length == 0 ? null : bytes;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
