using System.Globalization;

namespace TradingView.Client;

internal sealed class DataApi(TvExecutor executor) : IDataApi
{
    public const int MaxBars = 500;
    public const int MaxTrades = 20;

    public Task<QuoteResult> GetQuoteAsync(string? symbol = null, CancellationToken cancellationToken = default)
    {
        var command = string.IsNullOrWhiteSpace(symbol) ? TvCommand.Create("quote") : TvCommand.Create("quote", symbol);
        return executor.RunAsync(command, TvJsonContext.Default.QuoteResult, cancellationToken);
    }

    public Task<OhlcvResult> GetOhlcvAsync(int count = 100, CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("ohlcv", "-n", BarCount(count)), TvJsonContext.Default.OhlcvResult, cancellationToken);

    public Task<OhlcvSummaryResult> GetOhlcvSummaryAsync(int count = 100, CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("ohlcv", "-n", BarCount(count), "--summary"), TvJsonContext.Default.OhlcvSummaryResult, cancellationToken);

    public Task<StudyValuesResult> GetStudyValuesAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("values"), TvJsonContext.Default.StudyValuesResult, cancellationToken);

    public Task<PineLinesResult> GetPineLinesAsync(string? studyFilter = null, bool verbose = false, CancellationToken cancellationToken = default)
        => executor.RunAsync(DataCommand("lines", studyFilter, null, verbose), TvJsonContext.Default.PineLinesResult, cancellationToken);

    public Task<PineLabelsResult> GetPineLabelsAsync(string? studyFilter = null, int? maxLabels = null, bool verbose = false, CancellationToken cancellationToken = default)
    {
        if (maxLabels is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLabels), maxLabels, "Must be positive.");
        }

        return executor.RunAsync(DataCommand("labels", studyFilter, maxLabels, verbose), TvJsonContext.Default.PineLabelsResult, cancellationToken);
    }

    public Task<PineTablesResult> GetPineTablesAsync(string? studyFilter = null, CancellationToken cancellationToken = default)
        => executor.RunAsync(DataCommand("tables", studyFilter, null, false), TvJsonContext.Default.PineTablesResult, cancellationToken);

    public Task<PineBoxesResult> GetPineBoxesAsync(string? studyFilter = null, bool verbose = false, CancellationToken cancellationToken = default)
        => executor.RunAsync(DataCommand("boxes", studyFilter, null, verbose), TvJsonContext.Default.PineBoxesResult, cancellationToken);

    public Task<StrategyResultsResult> GetStrategyResultsAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("data", "strategy"), TvJsonContext.Default.StrategyResultsResult, cancellationToken);

    public Task<TradesResult> GetTradesAsync(int? maxTrades = null, CancellationToken cancellationToken = default)
    {
        if (maxTrades is < 1 or > MaxTrades)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTrades), maxTrades, $"Must be between 1 and {MaxTrades}.");
        }

        var command = maxTrades is { } max
            ? TvCommand.Create("data", "trades", "-n", max.ToString(CultureInfo.InvariantCulture))
            : TvCommand.Create("data", "trades");
        return executor.RunAsync(command, TvJsonContext.Default.TradesResult, cancellationToken);
    }

    public Task<EquityResult> GetEquityAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("data", "equity"), TvJsonContext.Default.EquityResult, cancellationToken);

    public Task<DepthResult> GetDepthAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("data", "depth"), TvJsonContext.Default.DepthResult, cancellationToken);

    public Task<IndicatorInfoResult> GetIndicatorAsync(string entityId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        return executor.RunAsync(TvCommand.Create("data", "indicator", entityId), TvJsonContext.Default.IndicatorInfoResult, cancellationToken);
    }

    private static string BarCount(int count)
        => count is >= 1 and <= MaxBars
            ? count.ToString(CultureInfo.InvariantCulture)
            : throw new ArgumentOutOfRangeException(nameof(count), count, $"Must be between 1 and {MaxBars}.");

    private static TvCommand DataCommand(string sub, string? filter, int? max, bool verbose)
    {
        var args = new List<string> { "data", sub };
        if (!string.IsNullOrWhiteSpace(filter))
        {
            args.Add("--filter");
            args.Add(filter);
        }

        if (max is { } n)
        {
            args.Add("--max");
            args.Add(n.ToString(CultureInfo.InvariantCulture));
        }

        if (verbose)
        {
            args.Add("--verbose");
        }

        return new TvCommand(args);
    }
}
