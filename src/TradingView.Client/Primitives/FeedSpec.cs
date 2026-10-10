namespace TradingView.Client;

/// <summary>
/// A <c>SYMBOL@TIMEFRAME</c> feed of <c>tv stream ohlcv</c>. The CLI matches both parts exactly against what
/// TradingView reports, so use the exchange-qualified symbol (<c>CME_MINI:ES1!</c>) and the canonical resolution.
/// </summary>
public sealed record FeedSpec
{
    /// <summary>Creates a feed with a validated timeframe.</summary>
    public FeedSpec(string symbol, Timeframe timeframe)
        : this(symbol, ValidTimeframe(timeframe), validated: true)
    {
    }

    /// <summary>Creates a feed with a raw interval string (for resolutions TradingView spells differently, e.g. "1D").</summary>
    public FeedSpec(string symbol, string interval)
        : this(symbol, interval, validated: true)
    {
    }

    private FeedSpec(string symbol, string interval, bool validated)
    {
        _ = validated;
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);
        Symbol = symbol.Trim();
        Interval = interval.Trim();
    }

    public string Symbol { get; }

    /// <summary>The timeframe text exactly as passed to the CLI.</summary>
    public string Interval { get; }

    /// <summary>Parses <c>SYMBOL@TIMEFRAME</c>, splitting at the last <c>@</c> like the CLI does.</summary>
    /// <exception cref="FormatException">There is no <c>@</c> or one side is empty.</exception>
    public static FeedSpec Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var at = text.LastIndexOf('@');
        if (at <= 0 || at == text.Length - 1 || string.IsNullOrWhiteSpace(text[..at]) || string.IsNullOrWhiteSpace(text[(at + 1)..]))
        {
            throw new FormatException($"'{text}' is not a feed spec. Use SYMBOL@TIMEFRAME, e.g. CME_MINI:ES1!@5.");
        }

        return new FeedSpec(text[..at], text[(at + 1)..], validated: true);
    }

    /// <summary>The timeframe as a <see cref="Client.Timeframe"/>, or null when <see cref="Interval"/> is not one.</summary>
    public Timeframe? Timeframe => Client.Timeframe.TryParse(Interval, out var tf) ? (Client.Timeframe?)tf : null;

    /// <summary>The wire form <c>SYMBOL@TIMEFRAME</c>.</summary>
    public override string ToString() => $"{Symbol}@{Interval}";

    private static string ValidTimeframe(Timeframe timeframe)
        => timeframe.Value.Length > 0 ? timeframe.Value : throw new ArgumentException("The timeframe is not set.", nameof(timeframe));
}
