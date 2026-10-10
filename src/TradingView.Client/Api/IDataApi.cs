namespace TradingView.Client;

/// <summary>Prices, bars, indicator values and Pine drawings (<c>tv quote|ohlcv|values|data *</c>).</summary>
public interface IDataApi
{
    /// <summary>Real-time quote. With <paramref name="symbol"/> the chart is switched to that symbol for a moment and restored.</summary>
    /// <exception cref="TvCommandException">The quote has no price.</exception>
    Task<QuoteResult> GetQuoteAsync(string? symbol = null, CancellationToken cancellationToken = default);

    /// <param name="count">1-500 bars (CLI maximum).</param>
    Task<OhlcvResult> GetOhlcvAsync(int count = 100, CancellationToken cancellationToken = default);

    /// <param name="count">1-500 bars the summary is computed over.</param>
    Task<OhlcvSummaryResult> GetOhlcvSummaryAsync(int count = 100, CancellationToken cancellationToken = default);

    /// <summary>Data-window values (formatted strings) of every visible study.</summary>
    Task<StudyValuesResult> GetStudyValuesAsync(CancellationToken cancellationToken = default);

    /// <summary>Price levels drawn with <c>line.new()</c>.</summary>
    /// <param name="studyFilter">Only studies whose name contains this text.</param>
    /// <param name="verbose">Include raw line objects.</param>
    Task<PineLinesResult> GetPineLinesAsync(string? studyFilter = null, bool verbose = false, CancellationToken cancellationToken = default);

    /// <param name="maxLabels">Labels per study (CLI default 50).</param>
    Task<PineLabelsResult> GetPineLabelsAsync(string? studyFilter = null, int? maxLabels = null, bool verbose = false, CancellationToken cancellationToken = default);

    Task<PineTablesResult> GetPineTablesAsync(string? studyFilter = null, CancellationToken cancellationToken = default);

    Task<PineBoxesResult> GetPineBoxesAsync(string? studyFilter = null, bool verbose = false, CancellationToken cancellationToken = default);

    /// <exception cref="TvOperationFailedException">There is no strategy on the chart (unless disabled via <see cref="TvClientOptions.ThrowOnOperationFailure"/>).</exception>
    Task<StrategyResultsResult> GetStrategyResultsAsync(CancellationToken cancellationToken = default);

    /// <param name="maxTrades">1-20 (CLI maximum).</param>
    /// <exception cref="TvOperationFailedException">There is no strategy or no trades.</exception>
    Task<TradesResult> GetTradesAsync(int? maxTrades = null, CancellationToken cancellationToken = default);

    /// <exception cref="TvOperationFailedException">The equity curve is not available (the common case).</exception>
    Task<EquityResult> GetEquityAsync(CancellationToken cancellationToken = default);

    /// <exception cref="TvCommandException">The DOM / depth panel is not open.</exception>
    Task<DepthResult> GetDepthAsync(CancellationToken cancellationToken = default);

    /// <param name="entityId">Study id from <see cref="ChartStateResult.Studies"/>.</param>
    /// <exception cref="TvCommandException">The study was not found.</exception>
    Task<IndicatorInfoResult> GetIndicatorAsync(string entityId, CancellationToken cancellationToken = default);
}
