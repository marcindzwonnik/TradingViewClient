using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>A saved chart layout.</summary>
public sealed record LayoutInfo : TvObject
{
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Id { get; init; }

    public string Name { get; init; } = string.Empty;
    public string? Symbol { get; init; }
    public string? Resolution { get; init; }

    /// <summary>Last modification as TradingView reports it (a date such as "2026-10-09").</summary>
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Modified { get; init; }
}

/// <summary>Result of <c>tv layout list</c>. A partial result carries <see cref="TvResult.Error"/> with <c>success: true</c>.</summary>
public sealed record LayoutListResult : TvResult
{
    public int LayoutCount { get; init; }
    public string? Source { get; init; }
    public IReadOnlyList<LayoutInfo> Layouts { get; init; } = [];
}

/// <summary>Result of <c>tv layout switch</c>.</summary>
public sealed record LayoutSwitchResult : TvResult
{
    public string? Layout { get; init; }

    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? LayoutId { get; init; }

    public string? Source { get; init; }
    public string? Action { get; init; }

    /// <summary>True when the CLI dismissed TradingView's "unsaved changes" prompt (discarding those changes).</summary>
    public bool UnsavedDialogDismissed { get; init; }
}

/// <summary>A chart pane. Either symbol/resolution or <see cref="Error"/> is set.</summary>
public sealed record PaneInfo : TvObject
{
    public int Index { get; init; }
    public string? Symbol { get; init; }
    public string? Resolution { get; init; }
    public string? Error { get; init; }
}

/// <summary>Result of <c>tv pane list</c>.</summary>
public sealed record PaneListResult : TvResult
{
    /// <summary>Layout code (e.g. "s", "2h", "4").</summary>
    public string Layout { get; init; } = string.Empty;

    public string LayoutName { get; init; } = string.Empty;

    /// <summary>Number of panes the layout shows.</summary>
    public int ChartCount { get; init; }

    public int? ActiveIndex { get; init; }

    /// <summary>All panes TradingView still reports; after shrinking a layout this can include stale panes beyond <see cref="ChartCount"/>.</summary>
    public IReadOnlyList<PaneInfo> Panes { get; init; } = [];

    /// <summary>The first <see cref="ChartCount"/> panes: the ones actually visible.</summary>
    [JsonIgnore]
    public IReadOnlyList<PaneInfo> VisiblePanes => Panes.Take(ChartCount).ToList();
}

/// <summary>Result of <c>tv pane layout CODE</c>.</summary>
public sealed record PaneLayoutResult : TvResult
{
    public string Layout { get; init; } = string.Empty;
    public string LayoutName { get; init; } = string.Empty;
    public int ChartCount { get; init; }
    public IReadOnlyList<PaneInfo> Panes { get; init; } = [];

    [JsonIgnore]
    public IReadOnlyList<PaneInfo> VisiblePanes => Panes.Take(ChartCount).ToList();
}

/// <summary>Result of <c>tv pane focus N</c>.</summary>
public sealed record PaneFocusResult : TvResult
{
    public int FocusedIndex { get; init; }
    public int TotalPanes { get; init; }
}

/// <summary>Result of <c>tv pane symbol N SYMBOL</c>.</summary>
public sealed record PaneSymbolResult : TvResult
{
    public int Index { get; init; }

    /// <summary>The symbol as requested (the pane may resolve it to another exchange, e.g. BATS:AAPL).</summary>
    public string Symbol { get; init; } = string.Empty;
}

/// <summary>A TradingView Desktop tab.</summary>
public sealed record TabInfo : TvObject
{
    public int Index { get; init; }

    /// <summary>CDP target id.</summary>
    public string Id { get; init; } = string.Empty;

    public string? Title { get; init; }
    public string? Url { get; init; }
    public string? ChartId { get; init; }
    public bool IsChart { get; init; }
}

/// <summary>Result of <c>tv tab list</c>.</summary>
public sealed record TabListResult : TvResult
{
    public int TabCount { get; init; }
    public IReadOnlyList<TabInfo> Tabs { get; init; } = [];
}

/// <summary>Result of <c>tv tab new</c>. The new tab shows TradingView's layout picker (<see cref="TabInfo.IsChart"/> is false).</summary>
public sealed record TabNewResult : TvResult
{
    public string? Action { get; init; }
    public int TabCount { get; init; }
    public IReadOnlyList<TabInfo> Tabs { get; init; } = [];
}

/// <summary>Result of <c>tv tab close</c>. <see cref="TvResult.Success"/> is false when the tab count did not drop.</summary>
public sealed record TabCloseResult : TvResult
{
    public string? Action { get; init; }
    public int TabsBefore { get; init; }
    public int TabsAfter { get; init; }
}

/// <summary>Result of <c>tv tab switch N</c>.</summary>
public sealed record TabSwitchResult : TvResult
{
    public string? Action { get; init; }
    public int Index { get; init; }
    public string? TabId { get; init; }
    public string? ChartId { get; init; }
    public bool VisuallySwitched { get; init; }
}

/// <summary>Result of <c>tv screenshot</c>.</summary>
public sealed record ScreenshotResult : TvResult
{
    public string? Method { get; init; }

    /// <summary>Absolute path of the PNG, inside the tradingview-cli checkout (<c>screenshots/</c>).</summary>
    public string FilePath { get; init; } = string.Empty;

    public string? Region { get; init; }
    public bool WaitedForRender { get; init; }
    public long SizeBytes { get; init; }
}
