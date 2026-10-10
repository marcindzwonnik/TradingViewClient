using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>An element matched by click/find.</summary>
public sealed record UiClickedElement : TvObject
{
    public bool Found { get; init; }
    public string? Tag { get; init; }
    public string? Text { get; init; }
    public string? AriaLabel { get; init; }
    public string? DataName { get; init; }
}

/// <summary>Result of <c>tv ui click</c>.</summary>
public sealed record UiClickResult : TvResult
{
    public UiClickedElement Clicked { get; init; } = new();
}

public sealed record UiHoveredElement : TvObject
{
    public string? By { get; init; }
    public string? Value { get; init; }
    public string? Tag { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
}

/// <summary>Result of <c>tv ui hover</c>. Coordinates are CSS pixels.</summary>
public sealed record UiHoverResult : TvResult
{
    public UiHoveredElement Hovered { get; init; } = new();
}

/// <summary>Result of <c>tv ui keyboard</c>.</summary>
public sealed record UiKeyResult : TvResult
{
    public string Key { get; init; } = string.Empty;
    public IReadOnlyList<string> Modifiers { get; init; } = [];
}

/// <summary>Result of <c>tv ui scroll</c>.</summary>
public sealed record UiScrollResult : TvResult
{
    public string? Direction { get; init; }
    public double Amount { get; init; }
}

public sealed record UiElement : TvObject
{
    public string? Tag { get; init; }
    public string? Text { get; init; }
    public string? AriaLabel { get; init; }
    public string? DataName { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool Visible { get; init; }
}

/// <summary>Result of <c>tv ui find</c> (at most 20 elements; coordinates are CSS pixels).</summary>
public sealed record UiFindResult : TvResult
{
    public string Query { get; init; } = string.Empty;
    public string? Strategy { get; init; }
    public int Count { get; init; }
    public IReadOnlyList<UiElement> Elements { get; init; } = [];
}

/// <summary>Result of <c>tv ui type</c>.</summary>
public sealed record UiTypeResult : TvResult
{
    /// <summary>The typed text, truncated to 100 characters by the CLI.</summary>
    public string Typed { get; init; } = string.Empty;

    public int Length { get; init; }
}

/// <summary>Result of <c>tv ui panel</c>.</summary>
public sealed record UiPanelResult : TvResult
{
    public string? Panel { get; init; }
    public string? Action { get; init; }
    public bool WasOpen { get; init; }

    /// <summary>"opened", "closed", "none", "already_open", "already_closed" or "unknown". The CLI can report "closed" without closing the Pine editor.</summary>
    public string? Performed { get; init; }
}

/// <summary>Result of <c>tv ui fullscreen</c>.</summary>
public sealed record UiFullscreenResult : TvResult
{
    public string? Action { get; init; }
}

/// <summary>Result of <c>tv ui mouse X Y</c>.</summary>
public sealed record UiMouseResult : TvResult
{
    public double X { get; init; }
    public double Y { get; init; }
    public string? Button { get; init; }

    [JsonPropertyName("double_click")]
    public bool DoubleClick { get; init; }
}

/// <summary>Result of <c>tv ui eval</c>. <see cref="Result"/> is null when the expression returned undefined or null.</summary>
public sealed record UiEvalResult : TvResult
{
    public JsonElement? Result { get; init; }
}
