using System.Globalization;

namespace TradingView.Client;

internal sealed class ChartApi(TvExecutor executor) : IChartApi
{
    public Task<ChartStateResult> GetStateAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("state"), TvJsonContext.Default.ChartStateResult, cancellationToken);

    public Task<SymbolResult> GetSymbolAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("symbol"), TvJsonContext.Default.SymbolResult, cancellationToken);

    public Task<SetSymbolResult> SetSymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return executor.RunAsync(TvCommand.Create("symbol", symbol), TvJsonContext.Default.SetSymbolResult, cancellationToken);
    }

    public Task<TimeframeResult> GetTimeframeAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("timeframe"), TvJsonContext.Default.TimeframeResult, cancellationToken);

    public Task<SetTimeframeResult> SetTimeframeAsync(Timeframe timeframe, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(timeframe.Value, nameof(timeframe));
        return executor.RunAsync(TvCommand.Create("timeframe", timeframe.Value), TvJsonContext.Default.SetTimeframeResult, cancellationToken);
    }

    public Task<ChartTypeResult> GetChartTypeAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("type"), TvJsonContext.Default.ChartTypeResult, cancellationToken);

    public Task<ChartTypeResult> SetChartTypeAsync(ChartType chartType, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(chartType))
        {
            throw new ArgumentOutOfRangeException(nameof(chartType), chartType, "Not a settable chart type.");
        }

        return executor.RunAsync(TvCommand.Create("type", chartType.ToString()), TvJsonContext.Default.ChartTypeResult, cancellationToken);
    }

    public Task<SymbolInfoResult> GetSymbolInfoAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("info"), TvJsonContext.Default.SymbolInfoResult, cancellationToken);

    public Task<SymbolSearchResult> SearchSymbolsAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return executor.RunAsync(TvCommand.Create("search", query), TvJsonContext.Default.SymbolSearchResult, cancellationToken);
    }

    public Task<VisibleRangeResult> GetVisibleRangeAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("range"), TvJsonContext.Default.VisibleRangeResult, cancellationToken);

    public Task<SetVisibleRangeResult> SetVisibleRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        if (to <= from)
        {
            throw new ArgumentException("'to' must be later than 'from'.", nameof(to));
        }

        var command = TvCommand.Create("range", "--from", Seconds(from), "--to", Seconds(to));
        return executor.RunAsync(command, TvJsonContext.Default.SetVisibleRangeResult, cancellationToken);
    }

    public Task<ScrollResult> ScrollToDateAsync(DateTimeOffset date, CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("scroll", Seconds(date)), TvJsonContext.Default.ScrollResult, cancellationToken);

    internal static string Seconds(DateTimeOffset value)
        => value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
}
