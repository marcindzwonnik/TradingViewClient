using System.Text.Json;
using System.Text.Json.Nodes;

namespace TradingView.Client;

/// <summary>Converts indicator input overrides to the JSON object the CLI expects after <c>-i</c>.</summary>
internal static class IndicatorInputs
{
    public static string ToJson(IReadOnlyDictionary<string, object?> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var node = new JsonObject();
        foreach (var (key, value) in inputs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            node[key] = ToNode(key, value);
        }

        return node.ToJsonString();
    }

    public static string ToJson(JsonObject inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        return inputs.ToJsonString();
    }

    private static JsonNode? ToNode(string key, object? value) => value switch
    {
        null => null,
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d when double.IsFinite(d) => JsonValue.Create(d),
        float f when float.IsFinite(f) => JsonValue.Create(f),
        decimal m => JsonValue.Create(m),
        JsonNode n => n.DeepClone(),
        JsonElement e => JsonValue.Create(e),
        Enum e => JsonValue.Create(e.ToString()),
        _ => throw new ArgumentException(
            $"Input '{key}' has unsupported type {value.GetType().Name}. Use string, bool, int, long, double, decimal, JsonNode or JsonElement.",
            nameof(value)),
    };
}
