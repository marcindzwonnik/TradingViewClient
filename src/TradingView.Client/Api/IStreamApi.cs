namespace TradingView.Client;

/// <summary>
/// Live streams (<c>tv stream *</c>) as <see cref="IAsyncEnumerable{T}"/>. Each enumeration starts one
/// <c>node</c> process; cancelling the token or disposing the enumerator kills it. Streams do not hold the
/// command semaphore, but they poll the same chart, so avoid changing symbol/timeframe while one runs.
/// If TradingView becomes unreachable the CLI retries silently; a failing process raises a <see cref="TvException"/>.
/// </summary>
public interface IStreamApi
{
    /// <summary>The forming bar of the active chart. <paramref name="interval"/> defaults to 300 ms.</summary>
    IAsyncEnumerable<QuoteTick> QuotesAsync(TimeSpan? interval = null, CancellationToken cancellationToken = default);

    /// <summary>The last bar, on each new bar or price change. Default interval 500 ms.</summary>
    IAsyncEnumerable<BarTick> BarsAsync(TimeSpan? interval = null, CancellationToken cancellationToken = default);

    /// <summary>Numeric study values. Default interval 500 ms.</summary>
    IAsyncEnumerable<StudyValuesTick> StudyValuesAsync(TimeSpan? interval = null, CancellationToken cancellationToken = default);

    /// <summary>Pine <c>line.new()</c> levels. Default interval 1 s.</summary>
    IAsyncEnumerable<PineLinesTick> PineLinesAsync(string? studyFilter = null, TimeSpan? interval = null, CancellationToken cancellationToken = default);

    /// <summary>Pine <c>label.new()</c> labels. Default interval 1 s.</summary>
    IAsyncEnumerable<PineLabelsTick> PineLabelsAsync(string? studyFilter = null, TimeSpan? interval = null, CancellationToken cancellationToken = default);

    /// <summary>Pine <c>table.new()</c> tables. Default interval 2 s.</summary>
    IAsyncEnumerable<PineTablesTick> PineTablesAsync(string? studyFilter = null, TimeSpan? interval = null, CancellationToken cancellationToken = default);

    /// <summary>Every pane of the layout. Default interval 500 ms.</summary>
    IAsyncEnumerable<AllPanesTick> AllPanesAsync(TimeSpan? interval = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Several <c>SYMBOL@TIMEFRAME</c> feeds at once. The CLI assigns feeds to panes, widening the layout and opening
    /// extra tabs when needed, and leaves them open afterwards. Feeds are validated before the process starts.
    /// </summary>
    /// <param name="feeds">At least one feed; duplicates are rejected.</param>
    /// <param name="interval">Poll interval, at least 100 ms (CLI default 250 ms).</param>
    IAsyncEnumerable<OhlcvFeedTick> OhlcvAsync(IEnumerable<FeedSpec> feeds, TimeSpan? interval = null, CancellationToken cancellationToken = default);
}
