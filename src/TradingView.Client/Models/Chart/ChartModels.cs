using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>An inclusive time range in unix seconds, exposed as timestamps.</summary>
public sealed record TimeRange : TvObject
{
    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset From { get; init; }

    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset To { get; init; }
}

/// <summary>A study (indicator) on the chart.</summary>
public sealed record StudyRef : TvObject
{
    /// <summary>Entity id used by the indicator commands.</summary>
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}

/// <summary>Result of <c>tv state</c>.</summary>
public sealed record ChartStateResult : TvResult
{
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Resolution in TradingView form (e.g. "5", "D").</summary>
    public string Resolution { get; init; } = string.Empty;

    /// <summary>Raw chart style id. See <see cref="ChartType"/> for ids 0-9; other ids exist.</summary>
    [JsonPropertyName("chartType")]
    public int ChartTypeId { get; init; }

    public IReadOnlyList<StudyRef> Studies { get; init; } = [];

    /// <summary>The style as <see cref="ChartType"/>, or null when it is not one of the ten settable styles.</summary>
    [JsonIgnore]
    public ChartType? ChartType => ChartTypeId is >= 0 and <= 9 ? (ChartType)ChartTypeId : null;

    /// <summary>The resolution parsed as a <see cref="Timeframe"/>, or null when it is not recognised.</summary>
    [JsonIgnore]
    public Timeframe? Timeframe => Client.Timeframe.TryParse(Resolution, out var tf) ? (Client.Timeframe?)tf : null;
}

/// <summary>Result of <c>tv symbol</c> (read).</summary>
public sealed record SymbolResult : TvResult
{
    public string Symbol { get; init; } = string.Empty;
    public string Resolution { get; init; } = string.Empty;
}

/// <summary>Result of <c>tv symbol SYMBOL</c>.</summary>
public sealed record SetSymbolResult : TvResult
{
    public string Symbol { get; init; } = string.Empty;

    /// <summary>False when the chart did not finish loading within the CLI's 10 s wait.</summary>
    public bool ChartReady { get; init; }
}

/// <summary>Result of <c>tv timeframe</c> (read).</summary>
public sealed record TimeframeResult : TvResult
{
    public string Resolution { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
}

/// <summary>Result of <c>tv timeframe TF</c>.</summary>
public sealed record SetTimeframeResult : TvResult
{
    public string Timeframe { get; init; } = string.Empty;

    /// <summary>False when the chart did not finish loading within the CLI's 10 s wait.</summary>
    public bool ChartReady { get; init; }
}

/// <summary>Result of <c>tv type</c> (read or set).</summary>
public sealed record ChartTypeResult : TvResult
{
    /// <summary>Style name, or the bare number as text for styles without a name (e.g. "19").</summary>
    [JsonPropertyName("chart_type")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string ChartTypeName { get; init; } = string.Empty;

    /// <summary>Raw TradingView chart style id.</summary>
    public int TypeNum { get; init; }

    /// <summary>The style as <see cref="Client.ChartType"/>, or null when it is not one of the ten settable styles.</summary>
    [JsonIgnore]
    public ChartType? Type => TypeNum is >= 0 and <= 9 ? (ChartType)TypeNum : null;
}

/// <summary>Result of <c>tv info</c>.</summary>
public sealed record SymbolInfoResult : TvResult
{
    public string? Symbol { get; init; }
    public string? FullName { get; init; }
    public string? Exchange { get; init; }
    public string? Description { get; init; }
    public string? Type { get; init; }
    public string? ProName { get; init; }
    public IReadOnlyList<string>? Typespecs { get; init; }
    public string? Resolution { get; init; }

    /// <summary>Raw chart style id.</summary>
    [JsonPropertyName("chart_type")]
    public int? ChartTypeId { get; init; }
}

/// <summary>One hit of <c>tv search</c>.</summary>
public sealed record SymbolSearchHit : TvObject
{
    public string Symbol { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Exchange { get; init; }
    public string? Type { get; init; }

    /// <summary>Exchange-qualified symbol, e.g. "NASDAQ:AAPL"; use it with SetSymbolAsync.</summary>
    public string FullName { get; init; } = string.Empty;
}

/// <summary>Result of <c>tv search QUERY</c> (REST lookup; at most 15 hits).</summary>
public sealed record SymbolSearchResult : TvResult
{
    public string Query { get; init; } = string.Empty;
    public string? Source { get; init; }
    public int Count { get; init; }
    public IReadOnlyList<SymbolSearchHit> Results { get; init; } = [];
}

/// <summary>Result of <c>tv range</c> (read).</summary>
public sealed record VisibleRangeResult : TvResult
{
    public TimeRange VisibleRange { get; init; } = new();

    /// <summary>
    /// Range of loaded bars. The upstream contract documents bar indices, but TradingView Desktop
    /// returns unix seconds here, so both ends are exposed as timestamps.
    /// </summary>
    public TimeRange BarsRange { get; init; } = new();
}

/// <summary>Result of <c>tv range --from --to</c>.</summary>
public sealed record SetVisibleRangeResult : TvResult
{
    public TimeRange Requested { get; init; } = new();
    public ActualRange Actual { get; init; } = new();
}

public sealed record ActualRange : TvObject
{
    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset From { get; init; }

    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset To { get; init; }

    /// <summary>Set when reading the resulting range failed.</summary>
    public string? Error { get; init; }
}

/// <summary>Result of <c>tv scroll DATE</c>.</summary>
public sealed record ScrollResult : TvResult
{
    /// <summary>The input as echoed by the CLI.</summary>
    public string Date { get; init; } = string.Empty;

    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset CenteredOn { get; init; }

    public string Resolution { get; init; } = string.Empty;

    /// <summary>The visible window requested around the date (25 bars each side).</summary>
    public TimeRange Window { get; init; } = new();
}
