using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>Reads a JSON string or number as a string (e.g. <c>chart_type</c> is a name, or a bare number for unnamed styles).</summary>
public sealed class FlexibleStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l)
                ? l.ToString(CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            _ => SkipToNull(ref reader),
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);

    private static string? SkipToNull(ref Utf8JsonReader reader)
    {
        reader.Skip();
        return null;
    }
}
