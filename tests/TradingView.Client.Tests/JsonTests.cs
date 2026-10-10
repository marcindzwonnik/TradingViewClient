using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingView.Client.Tests;

public class JsonTests
{
    public enum Sample
    {
        Unknown = 0,
        Candles = 1,
        HeikinAshi = 8,
        GreaterThan = 20,
    }

    private sealed record Timed
    {
        [JsonConverter(typeof(UnixSecondsConverter))]
        public DateTimeOffset Time { get; init; }

        [JsonConverter(typeof(UnixSecondsConverter))]
        public DateTimeOffset? OptionalTime { get; init; }

        [JsonConverter(typeof(UnixMillisecondsConverter))]
        public DateTimeOffset Ts { get; init; }
    }

    private static readonly JsonSerializerOptions EnumOptions = new()
    {
        Converters = { new TolerantEnumConverter<Sample>() },
    };

    [Fact]
    public void Unix_converters_read_seconds_and_milliseconds()
    {
        var model = JsonSerializer.Deserialize<Timed>(
            """{"Time":1700000000,"OptionalTime":null,"Ts":1700000000123}""")!;

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), model.Time);
        Assert.Null(model.OptionalTime);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000000123), model.Ts);
    }

    [Fact]
    public void Unix_converters_read_nullable_value_and_fractional_seconds()
    {
        var model = JsonSerializer.Deserialize<Timed>("""{"Time":1700000000.4,"OptionalTime":1700000001,"Ts":0}""")!;

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), model.Time);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000001), model.OptionalTime);
    }

    [Fact]
    public void Unix_converters_round_trip()
    {
        var original = new Timed { Time = DateTimeOffset.FromUnixTimeSeconds(42), Ts = DateTimeOffset.FromUnixTimeMilliseconds(4242) };

        var copy = JsonSerializer.Deserialize<Timed>(JsonSerializer.Serialize(original))!;

        Assert.Equal(original.Time, copy.Time);
        Assert.Equal(original.Ts, copy.Ts);
    }

    [Theory]
    [InlineData("\"Candles\"", Sample.Candles)]
    [InlineData("\"candles\"", Sample.Candles)]
    [InlineData("\"heikin_ashi\"", Sample.HeikinAshi)]
    [InlineData("\"heikin-ashi\"", Sample.HeikinAshi)]
    [InlineData("\"greater_than\"", Sample.GreaterThan)]
    [InlineData("8", Sample.HeikinAshi)]
    public void Tolerant_enum_reads_known_values(string json, Sample expected)
        => Assert.Equal(expected, JsonSerializer.Deserialize<Sample>(json, EnumOptions));

    [Theory]
    [InlineData("\"something_new\"")]
    [InlineData("19")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("{\"a\":1}")]
    [InlineData("[1,2]")]
    public void Tolerant_enum_maps_unknown_input_to_default(string json)
        => Assert.Equal(Sample.Unknown, JsonSerializer.Deserialize<Sample>(json, EnumOptions));

    [Fact]
    public void Tolerant_enum_writes_snake_case_names()
        => Assert.Equal("\"heikin_ashi\"", JsonSerializer.Serialize(Sample.HeikinAshi, EnumOptions));

    [Fact]
    public void Unknown_fields_are_kept_in_additional_data()
    {
        var status = JsonSerializer.Deserialize(
            """{"success":true,"cdp_connected":true,"brand_new_field":{"x":1}}""",
            TvJsonContext.Default.StatusResult)!;

        Assert.True(status.CdpConnected);
        Assert.NotNull(status.AdditionalData);
        Assert.Equal(1, status.AdditionalData["brand_new_field"].GetProperty("x").GetInt32());
    }
}
