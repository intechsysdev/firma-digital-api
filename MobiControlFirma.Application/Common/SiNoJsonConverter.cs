using System.Text.Json;
using System.Text.Json.Serialization;

namespace MobiControlFirma.Application.Common;

/// <summary>
/// Un sí o no como lo escribe quien integra: <c>"SI"</c>/<c>"NO"</c> (con o sin tilde), <c>"S"</c>/<c>"N"</c>,
/// <c>true</c>/<c>false</c> o <c>1</c>/<c>0</c>. Sale siempre como <c>"SI"</c> o <c>"NO"</c>, igual que se pidió.
/// </summary>
public sealed class SiNoJsonConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null,
            JsonTokenType.Number when reader.TryGetInt32(out var n) && n is 0 or 1 => n == 1,
            JsonTokenType.String => Texto(reader.GetString()),
            _ => throw new JsonException("Se esperaba SI o NO."),
        };

    private static bool? Texto(string? valor) =>
        valor?.Trim().ToUpperInvariant() switch
        {
            null or "" => null,
            "SI" or "SÍ" or "S" or "TRUE" or "1" => true,
            "NO" or "N" or "FALSE" or "0" => false,
            _ => throw new JsonException("Se esperaba SI o NO."),
        };

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value ? "SI" : "NO");
    }
}
