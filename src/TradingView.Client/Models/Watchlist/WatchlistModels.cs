using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>A watchlist row. Values are display strings ("7,811.54", "0.59%", "202.13 K") and null when TradingView shows nothing.</summary>
public sealed record WatchlistSymbol : TvObject
{
    public string Symbol { get; init; } = string.Empty;
    public string? Last { get; init; }
    public string? Change { get; init; }
    public string? ChangePercent { get; init; }
    public string? Volume { get; init; }

    [JsonIgnore]
    public double? LastValue => DataWindowNumber.TryParse(Last, out var v) ? v : null;

    [JsonIgnore]
    public double? ChangePercentValue => DataWindowNumber.TryParse(ChangePercent, out var v) ? v : null;
}

/// <summary>Result of <c>tv watchlist get</c> (the active list of the right panel).</summary>
public sealed record WatchlistResult : TvResult
{
    public int Count { get; init; }

    /// <summary>"dom_rows", "empty", "no_container" or "unknown".</summary>
    public string? Source { get; init; }

    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? ListId { get; init; }

    public string? ListName { get; init; }
    public IReadOnlyList<WatchlistSymbol> Symbols { get; init; } = [];
}

/// <summary>Result of <c>tv watchlist add</c>. The CLI adds any text, including symbols that do not exist.</summary>
public sealed record WatchlistAddResult : TvResult
{
    public string Symbol { get; init; } = string.Empty;
    public string? AddedAs { get; init; }

    /// <summary>"added" or "not_verified".</summary>
    public string? Action { get; init; }
}

public sealed record WatchlistBulkItem : TvObject
{
    public string Symbol { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? AddedAs { get; init; }
    public string? Error { get; init; }
}

/// <summary>Result of <c>tv watchlist add-bulk</c>.</summary>
public sealed record WatchlistAddBulkResult : TvResult
{
    public int Added { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<WatchlistBulkItem> Results { get; init; } = [];
}

/// <summary>Result of <c>tv watchlist remove</c>.</summary>
public sealed record WatchlistRemoveResult : TvResult
{
    public IReadOnlyList<string> Removed { get; init; } = [];

    /// <summary>Requested symbols that were not in the list.</summary>
    public IReadOnlyList<string> Skipped { get; init; } = [];

    public bool Verified { get; init; }

    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? ListId { get; init; }

    public string? ListName { get; init; }
    public string? Api { get; init; }
}
