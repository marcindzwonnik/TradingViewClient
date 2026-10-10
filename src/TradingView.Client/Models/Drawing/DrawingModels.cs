using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>A chart anchor: a moment and a price.</summary>
public sealed record ShapePoint(DateTimeOffset Time, double Price);

/// <summary>Input of <c>tv draw shape</c>. The CLI needs a time even for horizontal lines.</summary>
/// <param name="Shape">TradingView shape name (see <see cref="ShapeType"/> for the common ones).</param>
/// <param name="Point">First anchor.</param>
/// <param name="Point2">Second anchor for two-point shapes (trend line, rectangle, ...).</param>
/// <param name="Text">Text content for text-like shapes.</param>
/// <param name="Overrides">TradingView style overrides, e.g. <c>{"linecolor":"#ff0000"}</c>.</param>
public sealed record ShapeRequest(string Shape, ShapePoint Point, ShapePoint? Point2 = null, string? Text = null, JsonObject? Overrides = null)
{
    public ShapeRequest(ShapeType shape, ShapePoint point, ShapePoint? point2 = null, string? text = null, JsonObject? overrides = null)
        : this(Wire.Of(shape), point, point2, text, overrides)
    {
    }
}

/// <summary>Result of <c>tv draw shape</c>. <see cref="EntityId"/> is null when the id could not be determined.</summary>
public sealed record DrawShapeResult : TvResult
{
    public string Shape { get; init; } = string.Empty;
    public string? EntityId { get; init; }
}

public sealed record DrawingRef : TvObject
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

/// <summary>Result of <c>tv draw list</c>.</summary>
public sealed record DrawingListResult : TvResult
{
    public int Count { get; init; }
    public IReadOnlyList<DrawingRef> Shapes { get; init; } = [];
}

/// <summary>An anchor as stored by TradingView (the time snaps to the nearest bar).</summary>
public sealed record DrawingPoint : TvObject
{
    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset Time { get; init; }

    public double Price { get; init; }
}

/// <summary>Result of <c>tv draw get ID</c>.</summary>
public sealed record DrawingDetailsResult : TvResult
{
    public string EntityId { get; init; } = string.Empty;
    public IReadOnlyList<string>? AvailableMethods { get; init; }
    public IReadOnlyList<DrawingPoint>? Points { get; init; }
    public string? PointsError { get; init; }

    /// <summary>Style properties; the key set depends on the shape.</summary>
    public Dictionary<string, JsonElement>? Properties { get; init; }

    public string? PropertiesError { get; init; }
    public bool? Visible { get; init; }
    public bool? Locked { get; init; }
    public bool? Selectable { get; init; }
    public string? Name { get; init; }
}

/// <summary>Result of <c>tv draw remove ID</c>.</summary>
public sealed record DrawingRemoveResult : TvResult
{
    public string EntityId { get; init; } = string.Empty;
    public bool Removed { get; init; }
    public int RemainingShapes { get; init; }
}

/// <summary>Result of <c>tv draw clear</c> (removes every drawing on the chart).</summary>
public sealed record DrawingClearResult : TvResult
{
    public string? Action { get; init; }
}
