namespace TradingView.Client.IntegrationTests;

/// <summary>
/// Live streams. When the market is closed a stream emits one snapshot and then stays quiet, so every test
/// waits for the first tick only and then cancels (which must kill the node process).
/// </summary>
public class StreamIntegrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> FirstAsync<T>(IAsyncEnumerable<T> stream)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cts.CancelAfter(TimeSpan.FromSeconds(45));
        await foreach (var item in stream.WithCancellation(cts.Token))
        {
            return item;
        }

        throw new InvalidOperationException("The stream ended without emitting a tick.");
    }

    [Fact]
    public async Task Quote_stream_emits_the_forming_bar()
    {
        using var tv = await Live.CreateClientOrSkipAsync(Ct);
        var state = await tv.Chart.GetStateAsync(Ct);

        var tick = await FirstAsync(tv.Streams.QuotesAsync(cancellationToken: Ct));

        Assert.Equal(state.Symbol, tick.Symbol);
        Assert.True(tick.High >= tick.Low);
        Assert.True(tick.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.Equal("quote", tick.Stream);
        Assert.Empty(tick.AdditionalData ?? []);
    }

    [Fact]
    public async Task Bars_stream_matches_the_last_bar_of_ohlcv()
    {
        using var tv = await Live.CreateClientOrSkipAsync(Ct);
        var last = (await tv.Data.GetOhlcvAsync(1, Ct)).Bars[0];

        var tick = await FirstAsync(tv.Streams.BarsAsync(TimeSpan.FromMilliseconds(500), Ct));

        Assert.Equal(last.Time, tick.BarTime);
        Assert.Equal(last.Close, tick.Close);
    }

    [Fact]
    public async Task Study_values_and_pine_streams_emit_well_formed_ticks()
    {
        using var tv = await Live.CreateClientOrSkipAsync(Ct);

        var values = await FirstAsync(tv.Streams.StudyValuesAsync(cancellationToken: Ct));
        var lines = await FirstAsync(tv.Streams.PineLinesAsync(cancellationToken: Ct));
        var labels = await FirstAsync(tv.Streams.PineLabelsAsync(cancellationToken: Ct));
        var tables = await FirstAsync(tv.Streams.PineTablesAsync(cancellationToken: Ct));

        Assert.Equal(values.StudyCount, values.Studies.Count);
        Assert.Equal(lines.StudyCount, lines.Studies.Count);
        Assert.Equal(labels.StudyCount, labels.Studies.Count);
        Assert.Equal(tables.StudyCount, tables.Studies.Count);
    }

    [Fact]
    public async Task All_panes_stream_lists_the_layout()
    {
        using var tv = await Live.CreateClientOrSkipAsync(Ct);
        var panes = await tv.Panes.ListAsync(Ct);

        var tick = await FirstAsync(tv.Streams.AllPanesAsync(cancellationToken: Ct));

        Assert.Equal(panes.Layout, tick.Layout);
        Assert.Equal(panes.ChartCount, tick.PaneCount);
        Assert.NotEmpty(tick.Panes);
    }

    [Fact]
    public async Task Ohlcv_feed_for_the_active_chart_reuses_its_pane()
    {
        using var tv = await Live.CreateClientOrSkipAsync(Ct);
        var state = await tv.Chart.GetStateAsync(Ct);
        var feed = new FeedSpec(state.Symbol, state.Resolution);

        var tick = await FirstAsync(tv.Streams.OhlcvAsync([feed], TimeSpan.FromMilliseconds(250), Ct));

        Assert.Equal(state.Symbol, tick.Symbol);
        Assert.Equal(state.Resolution, tick.Timeframe);
        Assert.Equal(0, tick.PaneIndex);
        Assert.True(tick.ObservedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Cancelling_a_running_stream_ends_it_with_operation_canceled()
    {
        using var tv = await Live.CreateClientOrSkipAsync(Ct);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var received = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var unused in tv.Streams.QuotesAsync(cancellationToken: cts.Token))
            {
                received++;
                await cts.CancelAsync();
            }

            // A closed market gives no further tick: wait on the token instead of finishing normally.
            await Task.Delay(Timeout.Infinite, cts.Token);
        });

        Assert.Equal(1, received);
    }

    [Fact]
    public async Task Unreachable_cdp_makes_a_stream_fail_with_a_typed_exception()
    {
        using var live = await Live.CreateClientOrSkipAsync(Ct); // only meaningful when the CLI itself works
        using var tv = new TradingViewClient(new TvClientOptions { CdpPort = 1 });
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cts.CancelAfter(TimeSpan.FromSeconds(40));

        // The CLI retries connection errors silently, so the stream either fails fast or runs until cancelled;
        // in no case may it produce a tick.
        var ticks = 0;
        try
        {
            await foreach (var unused in tv.Streams.QuotesAsync(cancellationToken: cts.Token))
            {
                ticks++;
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or TvException)
        {
            // expected
        }

        Assert.Equal(0, ticks);
    }
}
