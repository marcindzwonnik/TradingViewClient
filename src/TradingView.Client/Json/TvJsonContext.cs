using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>Source-generated (AOT friendly) JSON metadata for every CLI response model.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StatusResult))]
[JsonSerializable(typeof(LaunchResult))]
[JsonSerializable(typeof(UpdateResult))]
[JsonSerializable(typeof(DiscoverResult))]
[JsonSerializable(typeof(UiStateResult))]
public sealed partial class TvJsonContext : JsonSerializerContext
{
}
