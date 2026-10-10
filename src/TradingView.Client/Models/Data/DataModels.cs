using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>One OHLCV bar.</summary>
public sealed record Bar : TvObject
{
    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset Time { get; init; }

    public double Open { get; init; }
    public double High { get; init; }
    public double Low { get; init; }
    public double Close { get; init; }

    /// <summary>0 when the symbol has no volume.</summary>
    public double Volume { get; init; }
}

/// <summary>Result of <c>tv quote</c>.</summary>
public sealed record QuoteResult : TvResult
{
    public string Symbol { get; init; } = string.Empty;

    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset? Time { get; init; }

    public double? Open { get; init; }
    public double? High { get; init; }
    public double? Low { get; init; }
    public double? Close { get; init; }

    /// <summary>Same as <see cref="Close"/>.</summary>
    public double? Last { get; init; }

    public double? Volume { get; init; }

    /// <summary>Scraped from the DOM; often absent.</summary>
    public double? Bid { get; init; }

    public double? Ask { get; init; }
    public double? HeaderPrice { get; init; }
    public string? Description { get; init; }
    public string? Exchange { get; init; }
    public string? Type { get; init; }
}

/// <summary>Result of <c>tv ohlcv -n N</c>.</summary>
public sealed record OhlcvResult : TvResult
{
    public int BarCount { get; init; }
    public int TotalAvailable { get; init; }
    public string? Source { get; init; }
    public IReadOnlyList<Bar> Bars { get; init; } = [];
}

/// <summary>Result of <c>tv ohlcv -n N --summary</c>.</summary>
public sealed record OhlcvSummaryResult : TvResult
{
    public int BarCount { get; init; }
    public TimeRange Period { get; init; } = new();
    public double Open { get; init; }
    public double Close { get; init; }
    public double High { get; init; }
    public double Low { get; init; }
    public double Range { get; init; }
    public double Change { get; init; }

    /// <summary>Percent change as formatted by the CLI, e.g. "-0.02%". See <see cref="ChangePercentValue"/>.</summary>
    public string ChangePct { get; init; } = string.Empty;

    public double AvgVolume { get; init; }
    [JsonPropertyName("last_5_bars")]
    public IReadOnlyList<Bar> Last5Bars { get; init; } = [];

    /// <summary><see cref="ChangePct"/> as a number (-0.02), or null when it cannot be parsed.</summary>
    [JsonIgnore]
    public double? ChangePercentValue => DataWindowNumber.TryParse(ChangePct, out var value) ? value : null;
}

/// <summary>Result of <c>tv values</c>: the data-window values of every visible study.</summary>
public sealed record StudyValuesResult : TvResult
{
    public int StudyCount { get; init; }
    public IReadOnlyList<StudyValues> Studies { get; init; } = [];
}

public sealed record StudyValues : TvObject
{
    /// <summary>Entity id, or null when TradingView does not expose it.</summary>
    public string? Id { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>Study inputs as raw JSON values (null when unavailable).</summary>
    public Dictionary<string, JsonElement>? Inputs { get; init; }

    /// <summary>Formatted display strings, e.g. "7,863.35" or "−0.32" (note the Unicode minus). Use <see cref="DataWindowNumber"/> to parse.</summary>
    public Dictionary<string, string> Values { get; init; } = [];
}

/// <summary>Result of <c>tv data lines</c>.</summary>
public sealed record PineLinesResult : TvResult
{
    public int StudyCount { get; init; }
    public IReadOnlyList<PineLinesStudy> Studies { get; init; } = [];
}

public sealed record PineLinesStudy : TvObject
{
    public string Name { get; init; } = string.Empty;
    public int TotalLines { get; init; }

    /// <summary>Prices of horizontal lines, descending.</summary>
    public IReadOnlyList<double> HorizontalLevels { get; init; } = [];

    /// <summary>Raw lines; present only with <c>verbose</c>.</summary>
    public IReadOnlyList<PineLine>? AllLines { get; init; }
}

public sealed record PineLine : TvObject
{
    public JsonElement? Id { get; init; }
    public double? Y1 { get; init; }
    public double? Y2 { get; init; }
    public JsonElement? X1 { get; init; }
    public JsonElement? X2 { get; init; }
    public bool Horizontal { get; init; }
    public JsonElement? Style { get; init; }
    public JsonElement? Width { get; init; }
    public JsonElement? Color { get; init; }
}

/// <summary>Result of <c>tv data labels</c>.</summary>
public sealed record PineLabelsResult : TvResult
{
    public int StudyCount { get; init; }
    public IReadOnlyList<PineLabelsStudy> Studies { get; init; } = [];
}

public sealed record PineLabelsStudy : TvObject
{
    public string Name { get; init; } = string.Empty;
    public int TotalLabels { get; init; }
    public int Showing { get; init; }
    public IReadOnlyList<PineLabel> Labels { get; init; } = [];
}

/// <summary>A Pine label. Only <see cref="Text"/> and <see cref="Price"/> are present unless <c>verbose</c> was requested.</summary>
public sealed record PineLabel : TvObject
{
    public string Text { get; init; } = string.Empty;
    public double? Price { get; init; }
    public JsonElement? Id { get; init; }
    public JsonElement? X { get; init; }
    public JsonElement? Yloc { get; init; }
    public JsonElement? Size { get; init; }

    [JsonPropertyName("textColor")]
    public JsonElement? TextColor { get; init; }

    public JsonElement? Color { get; init; }
}

/// <summary>Result of <c>tv data tables</c> (each table is a list of rows joined with " | ").</summary>
public sealed record PineTablesResult : TvResult
{
    public int StudyCount { get; init; }
    public IReadOnlyList<PineTablesStudy> Studies { get; init; } = [];
}

public sealed record PineTablesStudy : TvObject
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<PineTable> Tables { get; init; } = [];
}

public sealed record PineTable : TvObject
{
    /// <summary>Row text with cells joined by " | ".</summary>
    public IReadOnlyList<string> Rows { get; init; } = [];
}

/// <summary>Result of <c>tv data boxes</c>.</summary>
public sealed record PineBoxesResult : TvResult
{
    public int StudyCount { get; init; }
    public IReadOnlyList<PineBoxesStudy> Studies { get; init; } = [];
}

public sealed record PineBoxesStudy : TvObject
{
    public string Name { get; init; } = string.Empty;
    public int TotalBoxes { get; init; }
    public IReadOnlyList<PineZone> Zones { get; init; } = [];

    /// <summary>Raw boxes; present only with <c>verbose</c>.</summary>
    public IReadOnlyList<PineBox>? AllBoxes { get; init; }
}

public sealed record PineZone : TvObject
{
    public double High { get; init; }
    public double Low { get; init; }
}

public sealed record PineBox : TvObject
{
    public JsonElement? Id { get; init; }
    public double? High { get; init; }
    public double? Low { get; init; }
    public JsonElement? X1 { get; init; }
    public JsonElement? X2 { get; init; }

    [JsonPropertyName("borderColor")]
    public JsonElement? BorderColor { get; init; }

    [JsonPropertyName("bgColor")]
    public JsonElement? BgColor { get; init; }
}

/// <summary>Result of <c>tv data strategy</c>. <see cref="TvResult.Success"/> is false when no strategy metrics are available.</summary>
public sealed record StrategyResultsResult : TvResult
{
    public int MetricCount { get; init; }
    public string? Strategy { get; init; }
    public string? Currency { get; init; }
    public string? Source { get; init; }
    public StrategyMetrics Metrics { get; init; } = new();
    public IReadOnlyList<string>? UnhiddenStrategies { get; init; }
}

public sealed record StrategyMetrics : TvObject
{
    public double? NetProfit { get; init; }
    public double? NetProfitPercent { get; init; }
    public double? GrossProfit { get; init; }
    public double? GrossLoss { get; init; }
    public double? ProfitFactor { get; init; }
    public double? MaxDrawdown { get; init; }
    public double? MaxDrawdownPercent { get; init; }
    public double? TotalTrades { get; init; }
    public double? WinningTrades { get; init; }
    public double? LosingTrades { get; init; }
    public double? PercentProfitable { get; init; }
    public double? AvgTrade { get; init; }
    public double? LargestWin { get; init; }
    public double? LargestLoss { get; init; }
    public double? CommissionPaid { get; init; }
    public double? SharpeRatio { get; init; }
    public double? SortinoRatio { get; init; }
    public double? BuyHoldReturn { get; init; }
    public double? OpenPl { get; init; }
}

/// <summary>Result of <c>tv data trades</c> (at most 20 trades).</summary>
public sealed record TradesResult : TvResult
{
    public int TradeCount { get; init; }
    public int TotalOrders { get; init; }
    public string? Source { get; init; }
    public IReadOnlyList<StrategyTrade> Trades { get; init; } = [];
    public IReadOnlyList<string>? UnhiddenStrategies { get; init; }
}

public sealed record StrategyTrade : TvObject
{
    public JsonElement? Id { get; init; }
    public JsonElement? Type { get; init; }

    /// <summary>"buy" or "sell".</summary>
    public string? Side { get; init; }

    public JsonElement? Entry { get; init; }
    public double? Price { get; init; }
    public double? Qty { get; init; }
    public JsonElement? TimeIndex { get; init; }
}

/// <summary>Result of <c>tv data equity</c>. Usually unavailable: <see cref="TvResult.Success"/> is then false and <see cref="TvResult.Note"/> explains.</summary>
public sealed record EquityResult : TvResult
{
    public int DataPoints { get; init; }
    public string? Source { get; init; }
    public IReadOnlyList<JsonElement> Data { get; init; } = [];
    public int? BuyHoldPoints { get; init; }
    public IReadOnlyList<string>? UnhiddenStrategies { get; init; }
}

/// <summary>Result of <c>tv data depth</c> (needs the DOM panel to be open).</summary>
public sealed record DepthResult : TvResult
{
    public int BidLevels { get; init; }
    public int AskLevels { get; init; }
    public double? Spread { get; init; }
    public IReadOnlyList<DepthLevel> Bids { get; init; } = [];
    public IReadOnlyList<DepthLevel> Asks { get; init; } = [];
    public IReadOnlyList<double>? RawValues { get; init; }
}

public sealed record DepthLevel : TvObject
{
    public double Price { get; init; }
    public double Size { get; init; }
}

/// <summary>Result of <c>tv data indicator ID</c> / <c>tv indicator get ID</c>.</summary>
public sealed record IndicatorInfoResult : TvResult
{
    public string EntityId { get; init; } = string.Empty;
    public bool? Visible { get; init; }
    public IReadOnlyList<IndicatorInput>? Inputs { get; init; }
}

public sealed record IndicatorInput : TvObject
{
    public string Id { get; init; } = string.Empty;
    public JsonElement Value { get; init; }
}
