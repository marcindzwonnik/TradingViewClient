using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace TradingView.Client;

/// <summary>Chart drawings (<c>tv draw *</c>).</summary>
public interface IDrawingApi
{
    /// <summary>Draws a shape. Needs a time even for horizontal lines (the CLI requires it).</summary>
    Task<DrawShapeResult> DrawAsync(ShapeRequest request, CancellationToken cancellationToken = default);

    /// <param name="time">Anchor time; defaults to now.</param>
    Task<DrawShapeResult> DrawHorizontalLineAsync(double price, DateTimeOffset? time = null, JsonObject? overrides = null, CancellationToken cancellationToken = default);

    Task<DrawShapeResult> DrawTrendLineAsync(ShapePoint from, ShapePoint to, JsonObject? overrides = null, CancellationToken cancellationToken = default);

    Task<DrawShapeResult> DrawRectangleAsync(ShapePoint from, ShapePoint to, JsonObject? overrides = null, CancellationToken cancellationToken = default);

    Task<DrawShapeResult> DrawTextAsync(string text, ShapePoint at, JsonObject? overrides = null, CancellationToken cancellationToken = default);

    Task<DrawingListResult> ListAsync(CancellationToken cancellationToken = default);

    /// <exception cref="TvCommandException">The drawing does not exist.</exception>
    Task<DrawingDetailsResult> GetAsync(string entityId, CancellationToken cancellationToken = default);

    Task<DrawingRemoveResult> RemoveAsync(string entityId, CancellationToken cancellationToken = default);

    /// <summary>Removes every drawing on the chart, including ones the user drew by hand.</summary>
    Task<DrawingClearResult> ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>Alerts (<c>tv alert *</c>).</summary>
public interface IAlertApi
{
    Task<AlertListResult> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a price alert on the active symbol.</summary>
    /// <exception cref="TvOperationFailedException">TradingView rejected the alert.</exception>
    Task<AlertCreateResult> CreateAsync(double price, AlertCondition condition = AlertCondition.Crossing, string? message = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes one alert. The CLI reports success for unknown ids too; check with <see cref="ListAsync"/>.</summary>
    Task<AlertDeleteResult> DeleteAsync(long alertId, CancellationToken cancellationToken = default);

    /// <summary>Deletes EVERY alert of the account.</summary>
    Task<AlertDeleteResult> DeleteAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>The active watchlist (<c>tv watchlist *</c>).</summary>
public interface IWatchlistApi
{
    Task<WatchlistResult> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a symbol. The CLI does not validate it: unknown symbols are added too.</summary>
    Task<WatchlistAddResult> AddAsync(string symbol, CancellationToken cancellationToken = default);

    Task<WatchlistAddBulkResult> AddRangeAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default);

    /// <summary>Removes symbols; those not in the list are reported in <see cref="WatchlistRemoveResult.Skipped"/>.</summary>
    Task<WatchlistRemoveResult> RemoveAsync(params string[] symbols);

    /// <inheritdoc cref="RemoveAsync(string[])"/>
    Task<WatchlistRemoveResult> RemoveAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default);
}

/// <summary>Saved chart layouts (<c>tv layout *</c>).</summary>
public interface ILayoutApi
{
    Task<LayoutListResult> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Switches to a layout by name or id. If TradingView asks about unsaved changes the CLI dismisses the prompt.</summary>
    Task<LayoutSwitchResult> SwitchAsync(string nameOrId, CancellationToken cancellationToken = default);
}

/// <summary>Chart panes of the current layout (<c>tv pane *</c>).</summary>
public interface IPaneApi
{
    Task<PaneListResult> ListAsync(CancellationToken cancellationToken = default);

    Task<PaneLayoutResult> SetLayoutAsync(PaneLayout layout, CancellationToken cancellationToken = default);

    /// <exception cref="TvCommandException">The index is out of range.</exception>
    Task<PaneFocusResult> FocusAsync(int index, CancellationToken cancellationToken = default);

    Task<PaneSymbolResult> SetSymbolAsync(int index, string symbol, CancellationToken cancellationToken = default);
}

/// <summary>TradingView Desktop tabs (<c>tv tab *</c>).</summary>
public interface ITabApi
{
    Task<TabListResult> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens a new tab showing the layout picker.</summary>
    Task<TabNewResult> NewAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the tab that is active in TradingView's window (not necessarily the one the CLI is attached to).</summary>
    /// <exception cref="TvCommandException">Only one tab is open.</exception>
    Task<TabCloseResult> CloseAsync(CancellationToken cancellationToken = default);

    Task<TabSwitchResult> SwitchAsync(int index, CancellationToken cancellationToken = default);
}

/// <summary>Screenshots (<c>tv screenshot</c>).</summary>
public interface IScreenshotApi
{
    /// <summary>Saves a PNG under the tradingview-cli checkout and returns its path.</summary>
    /// <param name="fileName">File name without extension; the CLI strips path separators and "..".</param>
    Task<ScreenshotResult> CaptureAsync(ScreenshotRegion region = ScreenshotRegion.Full, string? fileName = null, CancellationToken cancellationToken = default);
}

/// <summary>Low-level UI automation inside TradingView (<c>tv ui *</c>). Coordinates are CSS pixels (not screenshot pixels).</summary>
public interface IUiApi
{
    /// <exception cref="TvCommandException">No element matched.</exception>
    Task<UiClickResult> ClickAsync(UiSelector by, string value, CancellationToken cancellationToken = default);

    Task<UiKeyResult> PressKeyAsync(string key, KeyModifiers modifiers = KeyModifiers.None, CancellationToken cancellationToken = default);

    Task<UiHoverResult> HoverAsync(UiSelector by, string value, CancellationToken cancellationToken = default);

    Task<UiScrollResult> ScrollAsync(ScrollDirection direction = ScrollDirection.Down, int? amountPixels = null, CancellationToken cancellationToken = default);

    Task<UiFindResult> FindAsync(string query, FindStrategy strategy = FindStrategy.Text, CancellationToken cancellationToken = default);

    /// <summary>Runs JavaScript in the TradingView page and returns the JSON result (a Null element for undefined).</summary>
    Task<JsonElement> EvaluateAsync(string expression, CancellationToken cancellationToken = default);

    /// <summary>Runs JavaScript and deserializes the JSON result with <paramref name="typeInfo"/> (AOT friendly).</summary>
    Task<T?> EvaluateAsync<T>(string expression, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default);

    /// <summary>Types into the focused input. Only the first 100 characters are echoed back.</summary>
    Task<UiTypeResult> TypeTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Opens, closes or toggles a panel. Closing the Pine editor this way is unreliable; click its Close button instead.</summary>
    Task<UiPanelResult> PanelAsync(PanelName panel, PanelAction action = PanelAction.Toggle, CancellationToken cancellationToken = default);

    Task<UiFullscreenResult> ToggleFullscreenAsync(CancellationToken cancellationToken = default);

    Task<UiMouseResult> MouseClickAsync(double x, double y, bool rightButton = false, bool doubleClick = false, CancellationToken cancellationToken = default);
}
