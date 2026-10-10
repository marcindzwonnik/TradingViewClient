using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>
/// One JSON line of a <c>tv stream *</c> command. Lines are emitted only when the data changed, so a quiet market
/// (or a closed one) yields a single snapshot and then nothing.
/// </summary>
public abstract record StreamTick : TvObject
{
    /// <summary>Moment the CLI produced the line (<c>_ts</c>, unix milliseconds on the wire).</summary>
    [JsonPropertyName("_ts")]
    [JsonConverter(typeof(UnixMillisecondsConverter))]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Stream label (<c>_stream</c>), e.g. "quote" or "all-panes".</summary>
    [JsonPropertyName("_stream")]
    public string? Stream { get; init; }
}

/// <summary><c>tv stream quote</c>: the forming bar of the active chart.</summary>
public sealed record QuoteTick : StreamTick
{
    public string Symbol { get; init; } = string.Empty;

    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset Time { get; init; }

    public double Open { get; init; }
    public double High { get; init; }
    public double Low { get; init; }
    public double Close { get; init; }
    public double Volume { get; init; }
}

/// <summary><c>tv stream bars</c>: the last bar, emitted on a new bar or a price change.</summary>
public sealed record BarTick : StreamTick
{
    public string Symbol { get; init; } = string.Empty;
    public string Resolution { get; init; } = string.Empty;

    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset BarTime { get; init; }

    public double Open { get; init; }
    public double High { get; init; }
    public double Low { get; init; }
    public double Close { get; init; }
    public double Volume { get; init; }
    public int BarIndex { get; init; }
}

/// <summary>Values of one study in a <see cref="StudyValuesTick"/>. Numbers (unlike the formatted strings of <c>tv values</c>); null for NaN.</summary>
public sealed record StudyValueSet : TvObject
{
    public string Name { get; init; } = string.Empty;
    public Dictionary<string, double?> Values { get; init; } = [];
}

/// <summary><c>tv stream values</c>: numeric study values of the active chart.</summary>
public sealed record StudyValuesTick : StreamTick
{
    public string Symbol { get; init; } = string.Empty;
    public int StudyCount { get; init; }
    public IReadOnlyList<StudyValueSet> Studies { get; init; } = [];
}

public sealed record StudyLevels : TvObject
{
    public string Study { get; init; } = string.Empty;
    public IReadOnlyList<double> Levels { get; init; } = [];
}

/// <summary><c>tv stream lines</c>: horizontal levels drawn by Pine scripts.</summary>
public sealed record PineLinesTick : StreamTick
{
    public string Symbol { get; init; } = string.Empty;
    public int StudyCount { get; init; }
    public IReadOnlyList<StudyLevels> Studies { get; init; } = [];
}

public sealed record StudyLabels : TvObject
{
    public string Study { get; init; } = string.Empty;
    public IReadOnlyList<PineLabel> Labels { get; init; } = [];
}

/// <summary><c>tv stream labels</c>: labels drawn by Pine scripts.</summary>
public sealed record PineLabelsTick : StreamTick
{
    public string Symbol { get; init; } = string.Empty;
    public int StudyCount { get; init; }
    public IReadOnlyList<StudyLabels> Studies { get; init; } = [];
}

/// <summary>A table of a stream tick: rows of cells (a 2D array, unlike the " | "-joined rows of <c>tv data tables</c>).</summary>
public sealed record StreamTable : TvObject
{
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
}

public sealed record StudyTables : TvObject
{
    public string Study { get; init; } = string.Empty;
    public IReadOnlyList<StreamTable> Tables { get; init; } = [];
}

/// <summary><c>tv stream tables</c>: tables drawn by Pine scripts.</summary>
public sealed record PineTablesTick : StreamTick
{
    public string Symbol { get; init; } = string.Empty;
    public int StudyCount { get; init; }
    public IReadOnlyList<StudyTables> Studies { get; init; } = [];
}

/// <summary>A pane in an <see cref="AllPanesTick"/>: price data, or <see cref="Error"/> (e.g. "no bars").</summary>
public sealed record PaneBar : TvObject
{
    public int Index { get; init; }
    public string? Symbol { get; init; }
    public string? Resolution { get; init; }

    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset? Time { get; init; }

    public double? Open { get; init; }
    public double? High { get; init; }
    public double? Low { get; init; }
    public double? Close { get; init; }
    public double? Volume { get; init; }
    public string? Error { get; init; }
}

/// <summary><c>tv stream all</c>: every pane of the layout at once.</summary>
public sealed record AllPanesTick : StreamTick
{
    public string Layout { get; init; } = string.Empty;
    public int PaneCount { get; init; }
    public IReadOnlyList<PaneBar> Panes { get; init; } = [];
}

/// <summary>
/// <c>tv stream ohlcv</c>: a bar of one <c>SYMBOL@TIMEFRAME</c> feed. This stream does not carry
/// <c>_ts</c>/<c>_stream</c>; <see cref="ObservedAt"/> plays that role.
/// </summary>
public sealed record OhlcvFeedTick : TvObject
{
    public int PaneIndex { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string Timeframe { get; init; } = string.Empty;

    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset BarTime { get; init; }

    public double Open { get; init; }
    public double High { get; init; }
    public double Low { get; init; }
    public double Close { get; init; }
    public double Volume { get; init; }
    public int BarIndex { get; init; }

    /// <summary>CDP target id of the tab the feed runs in.</summary>
    public string? TabId { get; init; }

    [JsonConverter(typeof(UnixMillisecondsConverter))]
    public DateTimeOffset ObservedAt { get; init; }
}
