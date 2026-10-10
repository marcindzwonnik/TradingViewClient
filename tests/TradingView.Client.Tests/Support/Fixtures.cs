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
        ["chart/state"] = TvJsonContext.Default.ChartStateResult,
        ["chart/symbol"] = TvJsonContext.Default.SymbolResult,
        ["chart/timeframe"] = TvJsonContext.Default.TimeframeResult,
        ["chart/type"] = TvJsonContext.Default.ChartTypeResult,
        ["chart/info"] = TvJsonContext.Default.SymbolInfoResult,
        ["chart/search"] = TvJsonContext.Default.SymbolSearchResult,
        ["chart/range"] = TvJsonContext.Default.VisibleRangeResult,
        ["data/quote"] = TvJsonContext.Default.QuoteResult,
        ["data/ohlcv"] = TvJsonContext.Default.OhlcvResult,
        ["data/ohlcv-summary"] = TvJsonContext.Default.OhlcvSummaryResult,
        ["data/values"] = TvJsonContext.Default.StudyValuesResult,
        ["data/lines"] = TvJsonContext.Default.PineLinesResult,
        ["data/lines-verbose"] = TvJsonContext.Default.PineLinesResult,
        ["data/labels"] = TvJsonContext.Default.PineLabelsResult,
        ["data/labels-verbose"] = TvJsonContext.Default.PineLabelsResult,
        ["data/tables"] = TvJsonContext.Default.PineTablesResult,
        ["data/boxes"] = TvJsonContext.Default.PineBoxesResult,
        ["data/boxes-verbose"] = TvJsonContext.Default.PineBoxesResult,
        ["data/strategy"] = TvJsonContext.Default.StrategyResultsResult,
        ["data/trades"] = TvJsonContext.Default.TradesResult,
        ["data/equity"] = TvJsonContext.Default.EquityResult,
        ["data/indicator"] = TvJsonContext.Default.IndicatorInfoResult,
        ["chart/set-symbol"] = TvJsonContext.Default.SetSymbolResult,
        ["chart/set-timeframe"] = TvJsonContext.Default.SetTimeframeResult,
        ["chart/set-type"] = TvJsonContext.Default.ChartTypeResult,
        ["chart/set-range"] = TvJsonContext.Default.SetVisibleRangeResult,
        ["chart/scroll"] = TvJsonContext.Default.ScrollResult,
        ["indicators/add"] = TvJsonContext.Default.IndicatorAddResult,
        ["indicators/add-unknown"] = TvJsonContext.Default.IndicatorAddResult,
        ["indicators/add-with-inputs"] = TvJsonContext.Default.IndicatorAddResult,
        ["indicators/get"] = TvJsonContext.Default.IndicatorInfoResult,
        ["indicators/set"] = TvJsonContext.Default.IndicatorSetInputsResult,
        ["indicators/toggle-hidden"] = TvJsonContext.Default.IndicatorVisibilityResult,
        ["indicators/toggle-visible"] = TvJsonContext.Default.IndicatorVisibilityResult,
        ["indicators/remove"] = TvJsonContext.Default.IndicatorRemoveResult,
        ["pine/list"] = TvJsonContext.Default.PineListResult,
        ["pine/analyze"] = TvJsonContext.Default.PineAnalyzeResult,
        ["pine/analyze-issues"] = TvJsonContext.Default.PineAnalyzeResult,
        ["pine/check"] = TvJsonContext.Default.PineCheckResult,
        ["pine/check-errors"] = TvJsonContext.Default.PineCheckResult,
        ["pine/new"] = TvJsonContext.Default.PineNewResult,
        ["pine/new-strategy"] = TvJsonContext.Default.PineNewResult,
        ["pine/set"] = TvJsonContext.Default.PineSetSourceResult,
        ["pine/set-invalid"] = TvJsonContext.Default.PineSetSourceResult,
        ["pine/get"] = TvJsonContext.Default.PineSourceResult,
        ["pine/compile"] = TvJsonContext.Default.PineCompileResult,
        ["pine/compile-errors"] = TvJsonContext.Default.PineCompileResult,
        ["pine/raw-compile"] = TvJsonContext.Default.PineRawCompileResult,
        ["pine/console"] = TvJsonContext.Default.PineConsoleResult,
        ["pine/errors"] = TvJsonContext.Default.PineErrorsResult,
        ["pine/errors-present"] = TvJsonContext.Default.PineErrorsResult,
        ["pine/open"] = TvJsonContext.Default.PineOpenResult,
        ["replay/status-stopped"] = TvJsonContext.Default.ReplayStatusResult,
        ["replay/status-started"] = TvJsonContext.Default.ReplayStatusResult,
        ["replay/status-in-position"] = TvJsonContext.Default.ReplayStatusResult,
        ["replay/start"] = TvJsonContext.Default.ReplayStartResult,
        ["replay/step"] = TvJsonContext.Default.ReplayStepResult,
        ["replay/step-2"] = TvJsonContext.Default.ReplayStepResult,
        ["replay/trade-buy"] = TvJsonContext.Default.ReplayTradeResult,
        ["replay/trade-close"] = TvJsonContext.Default.ReplayTradeResult,
        ["replay/autoplay-on"] = TvJsonContext.Default.ReplayAutoplayResult,
        ["replay/autoplay-off"] = TvJsonContext.Default.ReplayAutoplayResult,
        ["replay/stop"] = TvJsonContext.Default.ReplayStopResult,
        ["replay/stop-again"] = TvJsonContext.Default.ReplayStopResult,
    };

    /// <summary>Fixtures captured from a chart that has no strategy: the CLI answers exit 0 with <c>success: false</c>.</summary>
    public static IReadOnlySet<string> SoftFailures { get; } = new HashSet<string>
    {
        "data/strategy",
        "data/trades",
        "data/equity",
        "indicators/add-unknown",
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
