using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace TradingView.Client;

/// <summary>Parses numbers as TradingView formats them for display: "7,863.35", "−0.32" (Unicode minus), "-0.02%", "NaN".</summary>
public static class DataWindowNumber
{
    public static bool TryParse([NotNullWhen(true)] string? text, out double value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim()
            .Replace('−', '-') // Unicode minus
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(",", string.Empty)
            .TrimEnd('%');

        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.IsNaN(value))
        {
            value = default;
            return false;
        }

        return true;
    }
}
