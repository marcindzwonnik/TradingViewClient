namespace TradingView.Client.Tests;

public class TimeframeTests
{
    [Theory]
    [InlineData("1", "1")]
    [InlineData("5", "5")]
    [InlineData("240", "240")]
    [InlineData("15S", "15S")]
    [InlineData("15s", "15S")]
    [InlineData("D", "D")]
    [InlineData("d", "D")]
    [InlineData("1D", "D")]
    [InlineData("3D", "3D")]
    [InlineData("W", "W")]
    [InlineData("1W", "W")]
    [InlineData("M", "M")]
    [InlineData("12M", "12M")]
    [InlineData(" 60 ", "60")]
    public void Parse_normalizes_to_canonical_form(string input, string expected)
        => Assert.Equal(expected, Timeframe.Parse(input).Value);

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("S")]
    [InlineData("0D")]
    [InlineData("5X")]
    [InlineData("1h")]
    [InlineData("D5")]
    [InlineData("99999999999")]
    public void Parse_rejects_invalid_input(string input)
    {
        Assert.False(Timeframe.TryParse(input, out _));
        Assert.Throws<FormatException>(() => Timeframe.Parse(input));
    }

    [Fact]
    public void TryParse_rejects_null()
        => Assert.False(Timeframe.TryParse(null, out _));

    [Fact]
    public void Factories_produce_canonical_values()
    {
        Assert.Equal("30S", Timeframe.Seconds(30).Value);
        Assert.Equal("1", Timeframe.Minutes(1).Value);
        Assert.Equal("15", Timeframe.Minutes(15).Value);
        Assert.Equal("60", Timeframe.Hours(1).Value);
        Assert.Equal("240", Timeframe.Hours(4).Value);
        Assert.Equal("D", Timeframe.Days(1).Value);
        Assert.Equal("5D", Timeframe.Days(5).Value);
        Assert.Equal("W", Timeframe.Weeks(1).Value);
        Assert.Equal("2W", Timeframe.Weeks(2).Value);
        Assert.Equal("M", Timeframe.Months(1).Value);
        Assert.Equal("3M", Timeframe.Months(3).Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Factories_reject_non_positive_counts(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Timeframe.Seconds(count));
        Assert.Throws<ArgumentOutOfRangeException>(() => Timeframe.Minutes(count));
        Assert.Throws<ArgumentOutOfRangeException>(() => Timeframe.Hours(count));
        Assert.Throws<ArgumentOutOfRangeException>(() => Timeframe.Days(count));
        Assert.Throws<ArgumentOutOfRangeException>(() => Timeframe.Weeks(count));
        Assert.Throws<ArgumentOutOfRangeException>(() => Timeframe.Months(count));
    }

    [Fact]
    public void Hours_overflow_is_detected()
        => Assert.Throws<OverflowException>(() => Timeframe.Hours(int.MaxValue));

    [Fact]
    public void Equal_forms_compare_equal_and_string_converts_implicitly()
    {
        Timeframe fromString = "1D";

        Assert.Equal(Timeframe.Days(1), fromString);
        Assert.Equal(Timeframe.Hours(1), Timeframe.Minutes(60));
        Assert.Equal("D", fromString.ToString());
        Assert.Equal(string.Empty, default(Timeframe).ToString());
    }
}
