using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>
/// Enum converter that never throws on unknown input: unrecognised names or numbers map to <c>default(T)</c>
/// (by convention an <c>Unknown</c> member). Names match case-insensitively and ignore <c>_</c>/<c>-</c>.
/// </summary>
public sealed class TolerantEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    private static readonly Dictionary<string, T> ByName = BuildNames();

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return ByName.GetValueOrDefault(Normalize(reader.GetString()));
            case JsonTokenType.Number when reader.TryGetInt64(out var number):
                var value = (T)Enum.ToObject(typeof(T), number);
                return Enum.IsDefined(value) ? value : default;
            default:
                reader.Skip();
                return default;
        }
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteStringValue(ToSnakeCase(value.ToString()));

    private static Dictionary<string, T> BuildNames()
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var value in Enum.GetValues<T>())
        {
            map[Normalize(value.ToString())] = value;
        }

        return map;
    }

    private static string Normalize(string? text)
        => text is null ? string.Empty : text.Replace("_", "").Replace("-", "").ToLowerInvariant();

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                sb.Append('_');
            }

            sb.Append(char.ToLowerInvariant(name[i]));
        }

        return sb.ToString();
    }
}
