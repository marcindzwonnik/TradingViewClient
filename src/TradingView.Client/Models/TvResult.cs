using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>
/// Base of every deserialized CLI object. Fields the model does not know about are kept in
/// <see cref="AdditionalData"/> so that new CLI versions neither break deserialization nor lose data.
/// </summary>
public abstract record TvObject
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>Top-level CLI response: <c>{ success, error?, note? }</c> plus command specific fields.</summary>
public abstract record TvResult : TvObject
{
    public bool Success { get; init; }

    /// <summary>Error text. On a <c>success: true</c> result this marks a partial result (e.g. alert list).</summary>
    public string? Error { get; init; }

    public string? Note { get; init; }
}
