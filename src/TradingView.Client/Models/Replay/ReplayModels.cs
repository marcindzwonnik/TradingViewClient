using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>Result of <c>tv replay start</c>.</summary>
public sealed record ReplayStartResult : TvResult
{
    public bool ReplayStarted { get; init; }

    /// <summary>The requested date (yyyy-MM-dd) or "(first available)".</summary>
    public string? Date { get; init; }

    /// <summary>Replay position (unix seconds on the wire).</summary>
    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset? CurrentDate { get; init; }
}

/// <summary>Result of <c>tv replay step</c>.</summary>
public sealed record ReplayStepResult : TvResult
{
    public string? Action { get; init; }

    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset? CurrentDate { get; init; }
}

/// <summary>Result of <c>tv replay stop</c>.</summary>
public sealed record ReplayStopResult : TvResult
{
    /// <summary>"replay_stopped" or "already_stopped".</summary>
    public string? Action { get; init; }

    [JsonIgnore]
    public bool WasRunning => Action == "replay_stopped";
}

/// <summary>Result of <c>tv replay status</c>.</summary>
public sealed record ReplayStatusResult : TvResult
{
    public bool IsReplayAvailable { get; init; }
    public bool IsReplayStarted { get; init; }
    public bool IsAutoplayStarted { get; init; }

    /// <summary>Replay mode as reported by TradingView (e.g. "ActiveChart").</summary>
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? ReplayMode { get; init; }

    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset? CurrentDate { get; init; }

    /// <summary>Autoplay delay in milliseconds.</summary>
    public int AutoplayDelay { get; init; }

    /// <summary>Open position size; null when the replay trading API does not report it (observed even after a buy).</summary>
    public double? Position { get; init; }

    public double? RealizedPnl { get; init; }
}

/// <summary>Result of <c>tv replay autoplay</c> (a toggle).</summary>
public sealed record ReplayAutoplayResult : TvResult
{
    public bool AutoplayActive { get; init; }
    public int DelayMs { get; init; }
}

/// <summary>Result of <c>tv replay trade</c>.</summary>
public sealed record ReplayTradeResult : TvResult
{
    /// <summary>"buy", "sell" or "close".</summary>
    public string? Action { get; init; }

    public double? Position { get; init; }
    public double? RealizedPnl { get; init; }
}
