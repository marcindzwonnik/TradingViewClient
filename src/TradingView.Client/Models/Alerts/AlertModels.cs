using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>An alert of the account.</summary>
public sealed record AlertInfo : TvObject
{
    public long AlertId { get; init; }
    public string? Symbol { get; init; }

    /// <summary>Alert kind as TradingView names it (e.g. "price").</summary>
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Type { get; init; }

    public string? Message { get; init; }
    public bool Active { get; init; }

    /// <summary>The condition object; its shape depends on the alert type.</summary>
    public JsonElement? Condition { get; init; }

    public string? Resolution { get; init; }

    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Created { get; init; }

    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? LastFired { get; init; }

    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Expiration { get; init; }

    [JsonIgnore]
    public DateTimeOffset? CreatedAt => Parse(Created);

    [JsonIgnore]
    public DateTimeOffset? LastFiredAt => Parse(LastFired);

    [JsonIgnore]
    public DateTimeOffset? ExpiresAt => Parse(Expiration);

    private static DateTimeOffset? Parse(string? text)
        => DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var value) ? value : null;
}

/// <summary>Result of <c>tv alert list</c>. A partial result carries <see cref="TvResult.Error"/> with <c>success: true</c>.</summary>
public sealed record AlertListResult : TvResult
{
    public int AlertCount { get; init; }
    public string? Source { get; init; }
    public IReadOnlyList<AlertInfo> Alerts { get; init; } = [];
}

/// <summary>Result of <c>tv alert create</c>.</summary>
public sealed record AlertCreateResult : TvResult
{
    public string? Source { get; init; }
    public string? Symbol { get; init; }
    public double Price { get; init; }

    /// <summary>"cross", "greater" or "less" as normalised by the CLI.</summary>
    public string? Condition { get; init; }

    public string? Message { get; init; }

    /// <summary>Null when TradingView accepted the alert but did not return its id.</summary>
    public long? AlertId { get; init; }

    /// <summary>The server response text when creation failed.</summary>
    public string? Response { get; init; }
}

/// <summary>
/// Result of <c>tv alert delete</c>. The CLI reports <c>deleted_count: 1</c> even for ids that do not exist,
/// so verify with <c>ListAsync</c> when it matters.
/// </summary>
public sealed record AlertDeleteResult : TvResult
{
    public string? Source { get; init; }
    public int DeletedCount { get; init; }
    public IReadOnlyList<long>? AlertIds { get; init; }
    public string? Response { get; init; }
}
