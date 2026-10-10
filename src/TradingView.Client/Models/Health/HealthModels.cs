using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>Result of <c>tv status</c>.</summary>
public sealed record StatusResult : TvResult
{
    public bool CdpConnected { get; init; }
    public string? TargetId { get; init; }
    public string? TargetUrl { get; init; }
    public string? TargetTitle { get; init; }

    /// <summary>Active chart symbol, or "unknown" when the chart API is unavailable.</summary>
    public string? ChartSymbol { get; init; }

    /// <summary>Active chart resolution, or "unknown".</summary>
    public string? ChartResolution { get; init; }

    /// <summary>Raw TradingView chart style id (0-9 are documented, others exist, e.g. 19). Null when unavailable.</summary>
    [JsonPropertyName("chart_type")]
    public int? ChartTypeId { get; init; }

    public bool ApiAvailable { get; init; }

    /// <summary>Upstream CLI update check; present only when GitHub could be reached.</summary>
    public CliUpdateInfo? Update { get; init; }
}

public sealed record CliUpdateInfo : TvObject
{
    public bool UpdateAvailable { get; init; }
    public string? LocalCommit { get; init; }
    public string? LatestCommit { get; init; }
    public string? Hint { get; init; }
}

/// <summary>Result of <c>tv launch</c>. When CDP did not come up in time, <see cref="CdpReady"/> is false and <see cref="Warning"/> is set.</summary>
public sealed record LaunchResult : TvResult
{
    public string? Platform { get; init; }
    public string? Binary { get; init; }
    public int Pid { get; init; }
    public int CdpPort { get; init; }
    public string? CdpUrl { get; init; }
    public string? Browser { get; init; }
    public string? UserAgent { get; init; }

    /// <summary>False only when CDP was not reachable yet; absent (null) when it was.</summary>
    public bool? CdpReady { get; init; }

    public bool? MsixLocalCopy { get; init; }
    public string? Warning { get; init; }
}

/// <summary>Result of <c>tv update</c> (upstream CLI self-update).</summary>
public sealed record UpdateResult : TvResult
{
    public bool Updated { get; init; }
    public string? Status { get; init; }
    public string? Commit { get; init; }
    public string? Branch { get; init; }
    public IReadOnlyList<string>? ChangedFiles { get; init; }
    public string? FromCommit { get; init; }
    public string? ToCommit { get; init; }
    public int? CommitsPulled { get; init; }
    public bool? DepsInstalled { get; init; }
    public string? Warning { get; init; }
    public bool RestartRequired { get; init; }
}

/// <summary>Result of <c>tv discover</c>.</summary>
public sealed record DiscoverResult : TvResult
{
    public int ApisAvailable { get; init; }
    public int ApisTotal { get; init; }
    public DiscoveredApis? Apis { get; init; }
}

public sealed record DiscoveredApis : TvObject
{
    [JsonPropertyName("chartApi")]
    public ApiProbe? ChartApi { get; init; }
    [JsonPropertyName("chartWidgetCollection")]
    public ApiProbe? ChartWidgetCollection { get; init; }
    [JsonPropertyName("chartApiInstance")]
    public ApiProbe? ChartApiInstance { get; init; }
    [JsonPropertyName("bottomWidgetBar")]
    public ApiProbe? BottomWidgetBar { get; init; }
    [JsonPropertyName("replayApi")]
    public ApiProbe? ReplayApi { get; init; }
    [JsonPropertyName("alertService")]
    public ApiProbe? AlertService { get; init; }
}

public sealed record ApiProbe : TvObject
{
    public bool Available { get; init; }
    public string? Path { get; init; }

    [JsonPropertyName("methodCount")]
    public int? MethodCount { get; init; }

    public IReadOnlyList<string>? Methods { get; init; }
    public string? Error { get; init; }
}

/// <summary>Result of <c>tv ui-state</c>.</summary>
public sealed record UiStateResult : TvResult
{
    public PanelState? BottomPanel { get; init; }
    public PanelState? RightPanel { get; init; }
    public PanelState? PineEditor { get; init; }
    public PanelState? StrategyTester { get; init; }
    public PanelState? Widgetbar { get; init; }

    /// <summary>Visible buttons grouped by area (top_bar, toolbar, left_sidebar, ...).</summary>
    public Dictionary<string, List<UiButton>>? Buttons { get; init; }

    /// <summary>Pine editor action buttons (add_to_chart, save, ...).</summary>
    public Dictionary<string, UiKeyButton>? KeyButtons { get; init; }

    public UiChartInfo? Chart { get; init; }
    public UiReplayInfo? Replay { get; init; }
}

public sealed record PanelState : TvObject
{
    public bool Open { get; init; }
    public double? Height { get; init; }
    public double? Width { get; init; }
}

public sealed record UiButton : TvObject
{
    public string? Label { get; init; }
    public bool Disabled { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
}

public sealed record UiKeyButton : TvObject
{
    public string? Text { get; init; }
    public bool Disabled { get; init; }
    public bool Visible { get; init; }
}

/// <summary>Either chart details or an <see cref="Error"/>.</summary>
public sealed record UiChartInfo : TvObject
{
    public string? Symbol { get; init; }
    public string? Resolution { get; init; }

    [JsonPropertyName("chartType")]
    public int? ChartTypeId { get; init; }

    public int? StudyCount { get; init; }
    public string? Error { get; init; }
}

/// <summary>Either replay flags (arbitrary JSON values) or an <see cref="Error"/>.</summary>
public sealed record UiReplayInfo : TvObject
{
    public JsonElement? Available { get; init; }
    public JsonElement? Started { get; init; }
    public string? Error { get; init; }
}
