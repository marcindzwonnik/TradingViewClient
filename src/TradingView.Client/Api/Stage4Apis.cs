using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace TradingView.Client;

internal sealed class DrawingApi(TvExecutor executor) : IDrawingApi
{
    public Task<DrawShapeResult> DrawAsync(ShapeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Shape);

        // The "--name=value" form keeps values that start with '-' (negative prices, text) from being read as options.
        var args = new List<string>
        {
            "draw", "shape",
            $"--type={request.Shape}",
            $"--price={Wire.Number(request.Point.Price, "price")}",
            $"--time={Wire.Number(request.Point.Time.ToUnixTimeSeconds())}",
        };

        if (request.Point2 is { } second)
        {
            args.Add($"--price2={Wire.Number(second.Price, "price2")}");
            args.Add($"--time2={Wire.Number(second.Time.ToUnixTimeSeconds())}");
        }

        if (!string.IsNullOrEmpty(request.Text))
        {
            args.Add($"--text={request.Text}");
        }

        if (request.Overrides is { Count: > 0 } overrides)
        {
            args.Add($"--overrides={overrides.ToJsonString()}");
        }

        return executor.RunAsync(new TvCommand(args), TvJsonContext.Default.DrawShapeResult, cancellationToken);
    }

    public Task<DrawShapeResult> DrawHorizontalLineAsync(double price, DateTimeOffset? time = null, JsonObject? overrides = null, CancellationToken cancellationToken = default)
        => DrawAsync(new ShapeRequest(ShapeType.HorizontalLine, new ShapePoint(time ?? DateTimeOffset.UtcNow, price), overrides: overrides), cancellationToken);

    public Task<DrawShapeResult> DrawTrendLineAsync(ShapePoint from, ShapePoint to, JsonObject? overrides = null, CancellationToken cancellationToken = default)
        => DrawAsync(new ShapeRequest(ShapeType.TrendLine, from, to, overrides: overrides), cancellationToken);

    public Task<DrawShapeResult> DrawRectangleAsync(ShapePoint from, ShapePoint to, JsonObject? overrides = null, CancellationToken cancellationToken = default)
        => DrawAsync(new ShapeRequest(ShapeType.Rectangle, from, to, overrides: overrides), cancellationToken);

    public Task<DrawShapeResult> DrawTextAsync(string text, ShapePoint at, JsonObject? overrides = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return DrawAsync(new ShapeRequest(ShapeType.Text, at, text: text, overrides: overrides), cancellationToken);
    }

    public Task<DrawingListResult> ListAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("draw", "list"), TvJsonContext.Default.DrawingListResult, cancellationToken);

    public Task<DrawingDetailsResult> GetAsync(string entityId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        return executor.RunAsync(TvCommand.Create("draw", "get", entityId), TvJsonContext.Default.DrawingDetailsResult, cancellationToken);
    }

    public Task<DrawingRemoveResult> RemoveAsync(string entityId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        return executor.RunAsync(TvCommand.Create("draw", "remove", entityId), TvJsonContext.Default.DrawingRemoveResult, cancellationToken);
    }

    public Task<DrawingClearResult> ClearAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("draw", "clear"), TvJsonContext.Default.DrawingClearResult, cancellationToken);
}

internal sealed class AlertApi(TvExecutor executor) : IAlertApi
{
    public Task<AlertListResult> ListAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("alert", "list"), TvJsonContext.Default.AlertListResult, cancellationToken);

    public Task<AlertCreateResult> CreateAsync(double price, AlertCondition condition = AlertCondition.Crossing, string? message = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "alert", "create", $"--price={Wire.Number(price, nameof(price))}", $"--condition={Wire.Of(condition)}" };
        if (!string.IsNullOrEmpty(message))
        {
            args.Add($"--message={message}");
        }

        return executor.RunAsync(new TvCommand(args), TvJsonContext.Default.AlertCreateResult, cancellationToken);
    }

    public Task<AlertDeleteResult> DeleteAsync(long alertId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alertId);
        return executor.RunAsync(TvCommand.Create("alert", "delete", $"--id={Wire.Number(alertId)}"), TvJsonContext.Default.AlertDeleteResult, cancellationToken);
    }

    public Task<AlertDeleteResult> DeleteAllAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("alert", "delete", "--all"), TvJsonContext.Default.AlertDeleteResult, cancellationToken);
}

internal sealed class WatchlistApi(TvExecutor executor) : IWatchlistApi
{
    public Task<WatchlistResult> GetAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("watchlist", "get"), TvJsonContext.Default.WatchlistResult, cancellationToken);

    public Task<WatchlistAddResult> AddAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return executor.RunAsync(TvCommand.Create("watchlist", "add", symbol), TvJsonContext.Default.WatchlistAddResult, cancellationToken);
    }

    public Task<WatchlistAddBulkResult> AddRangeAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
        => executor.RunAsync(WithSymbols("add-bulk", symbols), TvJsonContext.Default.WatchlistAddBulkResult, cancellationToken);

    public Task<WatchlistRemoveResult> RemoveAsync(params string[] symbols) => RemoveAsync((IEnumerable<string>)symbols);

    public Task<WatchlistRemoveResult> RemoveAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
        => executor.RunAsync(WithSymbols("remove", symbols), TvJsonContext.Default.WatchlistRemoveResult, cancellationToken);

    private static TvCommand WithSymbols(string sub, IEnumerable<string> symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        var list = symbols.ToList();
        if (list.Count == 0 || list.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Provide at least one non-empty symbol.", nameof(symbols));
        }

        return new TvCommand(["watchlist", sub, .. list]);
    }
}

internal sealed class LayoutApi(TvExecutor executor) : ILayoutApi
{
    public Task<LayoutListResult> ListAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("layout", "list"), TvJsonContext.Default.LayoutListResult, cancellationToken);

    public Task<LayoutSwitchResult> SwitchAsync(string nameOrId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameOrId);
        return executor.RunAsync(TvCommand.Create("layout", "switch", nameOrId), TvJsonContext.Default.LayoutSwitchResult, cancellationToken);
    }
}

internal sealed class PaneApi(TvExecutor executor) : IPaneApi
{
    public Task<PaneListResult> ListAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("pane", "list"), TvJsonContext.Default.PaneListResult, cancellationToken);

    public Task<PaneLayoutResult> SetLayoutAsync(PaneLayout layout, CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("pane", "layout", Wire.Of(layout)), TvJsonContext.Default.PaneLayoutResult, cancellationToken);

    public Task<PaneFocusResult> FocusAsync(int index, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return executor.RunAsync(TvCommand.Create("pane", "focus", Wire.Number(index)), TvJsonContext.Default.PaneFocusResult, cancellationToken);
    }

    public Task<PaneSymbolResult> SetSymbolAsync(int index, string symbol, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return executor.RunAsync(TvCommand.Create("pane", "symbol", Wire.Number(index), symbol), TvJsonContext.Default.PaneSymbolResult, cancellationToken);
    }
}

internal sealed class TabApi(TvExecutor executor) : ITabApi
{
    public Task<TabListResult> ListAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("tab", "list"), TvJsonContext.Default.TabListResult, cancellationToken);

    public Task<TabNewResult> NewAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("tab", "new"), TvJsonContext.Default.TabNewResult, cancellationToken);

    public Task<TabCloseResult> CloseAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("tab", "close"), TvJsonContext.Default.TabCloseResult, cancellationToken);

    public Task<TabSwitchResult> SwitchAsync(int index, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return executor.RunAsync(TvCommand.Create("tab", "switch", Wire.Number(index)), TvJsonContext.Default.TabSwitchResult, cancellationToken);
    }
}

internal sealed class ScreenshotApi(TvExecutor executor) : IScreenshotApi
{
    public Task<ScreenshotResult> CaptureAsync(ScreenshotRegion region = ScreenshotRegion.Full, string? fileName = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "screenshot", "--region", Wire.Of(region) };
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            args.Add($"--output={fileName}");
        }

        return executor.RunAsync(new TvCommand(args), TvJsonContext.Default.ScreenshotResult, cancellationToken);
    }
}

internal sealed class UiApi(TvExecutor executor) : IUiApi
{
    public Task<UiClickResult> ClickAsync(UiSelector by, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        return executor.RunAsync(TvCommand.Create("ui", "click", "--by", Wire.Of(by), $"--value={value}"), TvJsonContext.Default.UiClickResult, cancellationToken);
    }

    public Task<UiKeyResult> PressKeyAsync(string key, KeyModifiers modifiers = KeyModifiers.None, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var args = new List<string> { "ui", "keyboard", key };
        if (modifiers.HasFlag(KeyModifiers.Ctrl))
        {
            args.Add("--ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            args.Add("--shift");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            args.Add("--alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            args.Add("--meta");
        }

        return executor.RunAsync(new TvCommand(args), TvJsonContext.Default.UiKeyResult, cancellationToken);
    }

    public Task<UiHoverResult> HoverAsync(UiSelector by, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        return executor.RunAsync(TvCommand.Create("ui", "hover", "--by", Wire.Of(by), $"--value={value}"), TvJsonContext.Default.UiHoverResult, cancellationToken);
    }

    public Task<UiScrollResult> ScrollAsync(ScrollDirection direction = ScrollDirection.Down, int? amountPixels = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "ui", "scroll", Wire.Of(direction) };
        if (amountPixels is { } amount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
            args.Add($"--amount={Wire.Number(amount)}");
        }

        return executor.RunAsync(new TvCommand(args), TvJsonContext.Default.UiScrollResult, cancellationToken);
    }

    public Task<UiFindResult> FindAsync(string query, FindStrategy strategy = FindStrategy.Text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return executor.RunAsync(TvCommand.Create("ui", "find", query, "--strategy", Wire.Of(strategy)), TvJsonContext.Default.UiFindResult, cancellationToken);
    }

    public async Task<JsonElement> EvaluateAsync(string expression, CancellationToken cancellationToken = default)
    {
        var result = await Evaluate(expression, cancellationToken).ConfigureAwait(false);
        return result.Result ?? NullElement;
    }

    public async Task<T?> EvaluateAsync<T>(string expression, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        var element = await EvaluateAsync(expression, cancellationToken).ConfigureAwait(false);
        return element.ValueKind == JsonValueKind.Null ? default : element.Deserialize(typeInfo);
    }

    public Task<UiTypeResult> TypeTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return executor.RunAsync(TvCommand.Create("ui", "type", text), TvJsonContext.Default.UiTypeResult, cancellationToken);
    }

    public Task<UiPanelResult> PanelAsync(PanelName panel, PanelAction action = PanelAction.Toggle, CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("ui", "panel", Wire.Of(panel), Wire.Of(action)), TvJsonContext.Default.UiPanelResult, cancellationToken);

    public Task<UiFullscreenResult> ToggleFullscreenAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("ui", "fullscreen"), TvJsonContext.Default.UiFullscreenResult, cancellationToken);

    public Task<UiMouseResult> MouseClickAsync(double x, double y, bool rightButton = false, bool doubleClick = false, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "ui", "mouse", Wire.Number(x, nameof(x)), Wire.Number(y, nameof(y)) };
        if (rightButton)
        {
            args.Add("--right");
        }

        if (doubleClick)
        {
            args.Add("--double");
        }

        return executor.RunAsync(new TvCommand(args), TvJsonContext.Default.UiMouseResult, cancellationToken);
    }

    private static readonly JsonElement NullElement = JsonDocument.Parse("null").RootElement.Clone();

    private Task<UiEvalResult> Evaluate(string expression, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        return executor.RunAsync(TvCommand.Create("ui", "eval", expression), TvJsonContext.Default.UiEvalResult, cancellationToken);
    }
}
