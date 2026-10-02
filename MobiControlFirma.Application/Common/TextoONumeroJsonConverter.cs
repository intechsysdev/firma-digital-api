using System.Text.Json;
using System.Text.Json.Serialization;

namespace MobiControlFirma.Application.Common;

/// <summary>
/// Acepta un identificador como texto o como número. Los sistemas de origen lo mandan como les
/// queda natural —el ID de un elemento de SharePoint es numérico, y Power Automate lo envía como
/// 1234 y no como "1234"—, y rechazarlo por eso sería un error de forma, no de contenido.
/// </summary>
public sealed class TextoONumeroJsonConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            // Tal como vino: 1234 sigue siendo "1234", sin pasar por double ni perder ceros.
            JsonTokenType.Number => System.Text.Encoding.UTF8.GetString(reader.ValueSpan),
            JsonTokenType.Null => null,
            _ => throw new JsonException("Se esperaba un texto o un número."),
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
