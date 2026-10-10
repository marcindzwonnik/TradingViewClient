using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace TradingView.Client;

internal sealed class StreamApi(TvExecutor executor) : IStreamApi
{
    public static readonly TimeSpan MinOhlcvInterval = TimeSpan.FromMilliseconds(100);

    public IAsyncEnumerable<QuoteTick> QuotesAsync(TimeSpan? interval = null, CancellationToken cancellationToken = default)
        => Stream(["stream", "quote", .. Interval(interval)], TvJsonContext.Default.QuoteTick, cancellationToken);

    public IAsyncEnumerable<BarTick> BarsAsync(TimeSpan? interval = null, CancellationToken cancellationToken = default)
        => Stream(["stream", "bars", .. Interval(interval)], TvJsonContext.Default.BarTick, cancellationToken);

    public IAsyncEnumerable<StudyValuesTick> StudyValuesAsync(TimeSpan? interval = null, CancellationToken cancellationToken = default)
        => Stream(["stream", "values", .. Interval(interval)], TvJsonContext.Default.StudyValuesTick, cancellationToken);

    public IAsyncEnumerable<PineLinesTick> PineLinesAsync(string? studyFilter = null, TimeSpan? interval = null, CancellationToken cancellationToken = default)
        => Stream(["stream", "lines", .. Filter(studyFilter), .. Interval(interval)], TvJsonContext.Default.PineLinesTick, cancellationToken);

    public IAsyncEnumerable<PineLabelsTick> PineLabelsAsync(string? studyFilter = null, TimeSpan? interval = null, CancellationToken cancellationToken = default)
        => Stream(["stream", "labels", .. Filter(studyFilter), .. Interval(interval)], TvJsonContext.Default.PineLabelsTick, cancellationToken);

    public IAsyncEnumerable<PineTablesTick> PineTablesAsync(string? studyFilter = null, TimeSpan? interval = null, CancellationToken cancellationToken = default)
        => Stream(["stream", "tables", .. Filter(studyFilter), .. Interval(interval)], TvJsonContext.Default.PineTablesTick, cancellationToken);

    public IAsyncEnumerable<AllPanesTick> AllPanesAsync(TimeSpan? interval = null, CancellationToken cancellationToken = default)
        => Stream(["stream", "all", .. Interval(interval)], TvJsonContext.Default.AllPanesTick, cancellationToken);

    public IAsyncEnumerable<OhlcvFeedTick> OhlcvAsync(IEnumerable<FeedSpec> feeds, TimeSpan? interval = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feeds);
        var specs = feeds.ToList();
        if (specs.Count == 0 || specs.Any(f => f is null))
        {
            throw new ArgumentException("Provide at least one feed.", nameof(feeds));
        }

        var duplicate = specs.GroupBy(f => f.ToString(), StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate feed '{duplicate.Key}'.", nameof(feeds));
        }

        if (interval is { } i && i < MinOhlcvInterval)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The multi-feed interval must be at least 100 ms.");
        }

        return Stream(["stream", "ohlcv", .. Interval(interval), .. specs.Select(f => f.ToString())], TvJsonContext.Default.OhlcvFeedTick, cancellationToken);
    }

    /// <summary>Validates the arguments eagerly (at call time) and defers the process start to the first MoveNextAsync.</summary>
    private IAsyncEnumerable<T> Stream<T>(string[] args, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
        => Run(new TvCommand(args), typeInfo, cancellationToken);

    private async IAsyncEnumerable<T> Run<T>(TvCommand command, JsonTypeInfo<T> typeInfo, [EnumeratorCancellation] CancellationToken cancellationToken)
        where T : class
    {
        await foreach (var line in executor.Transport.StreamLinesAsync(command, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            T? tick;
            try
            {
                tick = JsonSerializer.Deserialize(line, typeInfo);
            }
            catch (JsonException ex)
            {
                throw new TvResponseException($"'tv {command}' emitted a line that is not valid JSON for {typeof(T).Name}: {ex.Message}", line, ex);
            }

            yield return tick ?? throw new TvResponseException($"'tv {command}' emitted JSON null.", line);
        }
    }

    private static string[] Interval(TimeSpan? interval)
    {
        if (interval is not { } value)
        {
            return [];
        }

        var ms = value.TotalMilliseconds;
        if (ms < 1 || ms > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The interval must be between 1 ms and about 24 days.");
        }

        return [$"--interval={Math.Round(ms).ToString(CultureInfo.InvariantCulture)}"];
    }

    private static string[] Filter(string? studyFilter)
        => string.IsNullOrWhiteSpace(studyFilter) ? [] : [$"--filter={studyFilter}"];
}
