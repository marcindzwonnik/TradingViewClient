using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>Reads/writes a <see cref="DateTimeOffset"/> from a unix timestamp in seconds (bar times).</summary>
public sealed class UnixSecondsConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var seconds = reader.TryGetInt64(out var l) ? l : (long)Math.Round(reader.GetDouble());
        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.ToUnixTimeSeconds());
}

/// <summary>Reads/writes a <see cref="DateTimeOffset"/> from a unix timestamp in milliseconds (<c>_ts</c>, <c>observed_at</c>).</summary>
public sealed class UnixMillisecondsConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var millis = reader.TryGetInt64(out var l) ? l : (long)Math.Round(reader.GetDouble());
        return DateTimeOffset.FromUnixTimeMilliseconds(millis);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.ToUnixTimeMilliseconds());
}
