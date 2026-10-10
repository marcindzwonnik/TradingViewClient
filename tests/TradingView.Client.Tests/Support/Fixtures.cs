using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace TradingView.Client.Tests.Support;

/// <summary>Access to captured CLI responses and the registry mapping each fixture to its model.</summary>
internal static class Fixtures
{
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static string Read(string relativePath) => File.ReadAllText(Path.Combine(Root, relativePath));

    /// <summary>Fixture path (relative to Fixtures/, forward slashes, no extension) to its response type.</summary>
    public static IReadOnlyDictionary<string, JsonTypeInfo> Models { get; } = new Dictionary<string, JsonTypeInfo>
    {
        ["health/status"] = TvJsonContext.Default.StatusResult,
        ["health/discover"] = TvJsonContext.Default.DiscoverResult,
        ["health/ui-state"] = TvJsonContext.Default.UiStateResult,
    };

    public static TheoryData<string> AllJsonFixtures()
    {
        var data = new TheoryData<string>();
        foreach (var name in Models.Keys.Order())
        {
            data.Add(name);
        }

        return data;
    }

    public static TvResult Deserialize(string fixtureName)
    {
        var info = Models[fixtureName];
        var result = JsonSerializer.Deserialize(Read(fixtureName + ".json"), info);
        return (TvResult)(result ?? throw new InvalidOperationException($"Fixture {fixtureName} deserialized to null."));
    }

    /// <summary>Walks an object graph and returns the path of every <see cref="TvObject"/> that kept unknown JSON fields.</summary>
    public static List<string> FindUnmappedFields(object? root, string path = "$")
    {
        var found = new List<string>();
        Walk(root, path, found);
        return found;
    }

    private static void Walk(object? node, string path, List<string> found)
    {
        switch (node)
        {
            case null:
            case string:
                return;
            case TvObject tv:
                if (tv.AdditionalData is { Count: > 0 } extra)
                {
                    found.Add($"{path} has unmapped fields: {string.Join(", ", extra.Keys)}");
                }

                foreach (var property in tv.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.GetIndexParameters().Length == 0 && property.Name != nameof(TvObject.AdditionalData))
                    {
                        Walk(property.GetValue(tv), $"{path}.{property.Name}", found);
                    }
                }

                return;
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    Walk(entry.Value, $"{path}[{entry.Key}]", found);
                }

                return;
            case IEnumerable enumerable:
                var index = 0;
                foreach (var item in enumerable)
                {
                    Walk(item, $"{path}[{index++}]", found);
                }

                return;
        }
    }
}
