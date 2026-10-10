using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace TradingView.Client;

/// <summary>
/// A TradingView chart resolution in its canonical wire form: <c>"30S"</c>, <c>"5"</c> (minutes), <c>"240"</c>,
/// <c>"D"</c>, <c>"3D"</c>, <c>"W"</c>, <c>"M"</c>. Validated on construction, so a bad value never reaches the CLI.
/// </summary>
public readonly record struct Timeframe
{
    private readonly string? _value;

    private Timeframe(string value) => _value = value;

    /// <summary>The canonical resolution string (e.g. <c>"60"</c>, <c>"D"</c>). Empty for <c>default</c>.</summary>
    public string Value => _value ?? string.Empty;

    public static Timeframe Seconds(int count) => new($"{Positive(count)}S");

    public static Timeframe Minutes(int count) => new(Positive(count).ToString(CultureInfo.InvariantCulture));

    public static Timeframe Hours(int count) => new(checked(Positive(count) * 60).ToString(CultureInfo.InvariantCulture));

    public static Timeframe Days(int count) => Unit(count, 'D');

    public static Timeframe Weeks(int count) => Unit(count, 'W');

    public static Timeframe Months(int count) => Unit(count, 'M');

    /// <summary>Parses TradingView resolutions: <c>N</c> (minutes), <c>NS</c>, <c>[N]D</c>, <c>[N]W</c>, <c>[N]M</c>; <c>"1D"</c> normalizes to <c>"D"</c>.</summary>
    /// <exception cref="FormatException">The text is not a valid resolution.</exception>
    public static Timeframe Parse(string text)
        => TryParse(text, out var result) ? result : throw new FormatException($"'{text}' is not a valid TradingView timeframe (examples: 15S, 1, 60, D, 3D, W, M).");

    public static bool TryParse([NotNullWhen(true)] string? text, out Timeframe result)
    {
        result = default;
        var s = text?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }

        var last = s[^1];
        if (char.IsAsciiDigit(last))
        {
            if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes <= 0)
            {
                return false;
            }

            result = Minutes(minutes);
            return true;
        }

        if (last is not ('S' or 'D' or 'W' or 'M'))
        {
            return false;
        }

        var digits = s[..^1];
        var count = 1;
        if (digits.Length > 0 && (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count <= 0))
        {
            return false;
        }

        if (last == 'S' && digits.Length == 0)
        {
            return false; // a bare "S" is not a resolution
        }

        result = last == 'S' ? Seconds(count) : Unit(count, last);
        return true;
    }

    public static implicit operator Timeframe(string text) => Parse(text);

    /// <summary>The canonical resolution string.</summary>
    public override string ToString() => Value;

    private static Timeframe Unit(int count, char unit)
        => new(Positive(count) == 1 ? unit.ToString() : $"{count}{unit}");

    private static int Positive(int count)
        => count > 0 ? count : throw new ArgumentOutOfRangeException(nameof(count), count, "The count must be positive.");
}
