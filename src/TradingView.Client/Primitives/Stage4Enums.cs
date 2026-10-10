using System.Globalization;

namespace TradingView.Client;

/// <summary>Drawing tools accepted by <c>tv draw shape</c>. Any other TradingView shape name can be passed as a string.</summary>
public enum ShapeType
{
    HorizontalLine,
    HorizontalRay,
    VerticalLine,
    TrendLine,
    Ray,
    ExtendedLine,
    Arrow,
    Rectangle,
    Circle,
    Ellipse,
    Triangle,
    ParallelChannel,
    FibRetracement,
    LongPosition,
    ShortPosition,
    Text,
    Callout,
    Note,
    PriceLabel,
    Flag,
}

/// <summary>Condition of a price alert (<c>tv alert create --condition</c>).</summary>
public enum AlertCondition
{
    Crossing,
    GreaterThan,
    LessThan,
}

/// <summary>Chart grid layouts (<c>tv pane layout</c>). Plan-limited accounts support fewer panes.</summary>
public enum PaneLayout
{
    Single,
    TwoHorizontal,
    TwoVertical,
    TwoOne,
    OneTwo,
    ThreeHorizontal,
    ThreeVertical,
    ThreeSplit,
    Grid2x2,
    FourHorizontal,
    FourVertical,
    FourSplit,
    Six,
    Eight,
    Ten,
    Twelve,
    Fourteen,
    Sixteen,
}

/// <summary>Region captured by <c>tv screenshot</c>.</summary>
public enum ScreenshotRegion
{
    Full,
    Chart,
    StrategyTester,
}

/// <summary>How <c>tv ui click</c> / <c>hover</c> locate an element.</summary>
public enum UiSelector
{
    Text,
    AriaLabel,
    DataName,
    ClassContains,
}

/// <summary>How <c>tv ui find</c> searches.</summary>
public enum FindStrategy
{
    Text,
    AriaLabel,
    Css,
}

public enum ScrollDirection
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>Panels that <c>tv ui panel</c> can open and close.</summary>
public enum PanelName
{
    PineEditor,
    StrategyTester,
    Watchlist,
    Alerts,
    Trading,
}

public enum PanelAction
{
    Toggle,
    Open,
    Close,
}

/// <summary>Modifier keys for <c>tv ui keyboard</c>.</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Meta = 8,
}

/// <summary>Wire (CLI) spellings of the enums above.</summary>
internal static class Wire
{
    public static string Of(ShapeType value) => value switch
    {
        ShapeType.HorizontalLine => "horizontal_line",
        ShapeType.HorizontalRay => "horizontal_ray",
        ShapeType.VerticalLine => "vertical_line",
        ShapeType.TrendLine => "trend_line",
        ShapeType.Ray => "ray",
        ShapeType.ExtendedLine => "extended",
        ShapeType.Arrow => "arrow",
        ShapeType.Rectangle => "rectangle",
        ShapeType.Circle => "circle",
        ShapeType.Ellipse => "ellipse",
        ShapeType.Triangle => "triangle",
        ShapeType.ParallelChannel => "parallel_channel",
        ShapeType.FibRetracement => "fib_retracement",
        ShapeType.LongPosition => "long_position",
        ShapeType.ShortPosition => "short_position",
        ShapeType.Text => "text",
        ShapeType.Callout => "callout",
        ShapeType.Note => "note",
        ShapeType.PriceLabel => "price_label",
        ShapeType.Flag => "flag",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown shape type."),
    };

    public static string Of(AlertCondition value) => value switch
    {
        AlertCondition.Crossing => "crossing",
        AlertCondition.GreaterThan => "greater_than",
        AlertCondition.LessThan => "less_than",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown alert condition."),
    };

    public static string Of(PaneLayout value) => value switch
    {
        PaneLayout.Single => "s",
        PaneLayout.TwoHorizontal => "2h",
        PaneLayout.TwoVertical => "2v",
        PaneLayout.TwoOne => "2-1",
        PaneLayout.OneTwo => "1-2",
        PaneLayout.ThreeHorizontal => "3h",
        PaneLayout.ThreeVertical => "3v",
        PaneLayout.ThreeSplit => "3s",
        PaneLayout.Grid2x2 => "4",
        PaneLayout.FourHorizontal => "4h",
        PaneLayout.FourVertical => "4v",
        PaneLayout.FourSplit => "4s",
        PaneLayout.Six => "6",
        PaneLayout.Eight => "8",
        PaneLayout.Ten => "10",
        PaneLayout.Twelve => "12",
        PaneLayout.Fourteen => "14",
        PaneLayout.Sixteen => "16",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown pane layout."),
    };

    public static string Of(ScreenshotRegion value) => value switch
    {
        ScreenshotRegion.Full => "full",
        ScreenshotRegion.Chart => "chart",
        ScreenshotRegion.StrategyTester => "strategy_tester",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown screenshot region."),
    };

    public static string Of(UiSelector value) => value switch
    {
        UiSelector.Text => "text",
        UiSelector.AriaLabel => "aria-label",
        UiSelector.DataName => "data-name",
        UiSelector.ClassContains => "class-contains",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown selector."),
    };

    public static string Of(FindStrategy value) => value switch
    {
        FindStrategy.Text => "text",
        FindStrategy.AriaLabel => "aria-label",
        FindStrategy.Css => "css",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown strategy."),
    };

    public static string Of(ScrollDirection value) => value switch
    {
        ScrollDirection.Up => "up",
        ScrollDirection.Down => "down",
        ScrollDirection.Left => "left",
        ScrollDirection.Right => "right",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown direction."),
    };

    public static string Of(PanelName value) => value switch
    {
        PanelName.PineEditor => "pine-editor",
        PanelName.StrategyTester => "strategy-tester",
        PanelName.Watchlist => "watchlist",
        PanelName.Alerts => "alerts",
        PanelName.Trading => "trading",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown panel."),
    };

    public static string Of(PanelAction value) => value switch
    {
        PanelAction.Toggle => "toggle",
        PanelAction.Open => "open",
        PanelAction.Close => "close",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown panel action."),
    };

    /// <summary>Invariant, round-trippable number; rejects NaN and infinities before they reach the CLI.</summary>
    public static string Number(double value, string name)
        => double.IsFinite(value)
            ? value.ToString("R", CultureInfo.InvariantCulture)
            : throw new ArgumentOutOfRangeException(name, value, "Must be a finite number.");

    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
