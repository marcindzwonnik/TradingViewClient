using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using TradingView.Client.Tests.Support;

namespace TradingView.Client.Tests;

public class StreamTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Captured stream fixtures (JSONL) and the tick type of every line.</summary>
    public static IReadOnlyDictionary<string, JsonTypeInfo> JsonlModels { get; } = new Dictionary<string, JsonTypeInfo>
    {
        ["streams/quote"] = TvJsonContext.Default.QuoteTick,
        ["streams/bars"] = TvJsonContext.Default.BarTick,
        ["streams/values"] = TvJsonContext.Default.StudyValuesTick,
        ["streams/lines"] = TvJsonContext.Default.PineLinesTick,
        ["streams/labels"] = TvJsonContext.Default.PineLabelsTick,
        ["streams/tables"] = TvJsonContext.Default.PineTablesTick,
        ["streams/all"] = TvJsonContext.Default.AllPanesTick,
        ["streams/ohlcv"] = TvJsonContext.Default.OhlcvFeedTick,
    };

    public static TheoryData<string> JsonlFixtures()
    {
        var data = new TheoryData<string>();
        foreach (var name in JsonlModels.Keys.Order())
        {
            data.Add(name);
        }

        return data;
    }

    private static IEnumerable<string> Lines(string fixture)
        => Fixtures.Read(fixture + ".jsonl").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [Fact]
    public void Every_jsonl_fixture_is_registered()
    {
        var onDisk = Directory.EnumerateFiles(Fixtures.Root, "*.jsonl", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Fixtures.Root, f).Replace('\\', '/')[..^".jsonl".Length])
            .Order();

        Assert.Equal(JsonlModels.Keys.Order(), onDisk);
    }

    /// <summary>Contract drift detector for streams: no JSON field of any captured line may be left unmapped.</summary>
    [Theory]
    [MemberData(nameof(JsonlFixtures))]
    public void Stream_fixture_lines_deserialize_without_unmapped_fields(string fixture)
    {
        var lines = Lines(fixture).ToList();
        Assert.NotEmpty(lines);

        foreach (var line in lines)
        {
            var tick = JsonSerializer.Deserialize(line, JsonlModels[fixture]);
            Assert.NotNull(tick);
            Assert.Empty(Fixtures.FindUnmappedFields(tick));
        }
    }

    [Fact]
    public void Quote_bars_and_all_ticks_map_values_and_times()
    {
        var quote = JsonSerializer.Deserialize(Lines("streams/quote").First(), TvJsonContext.Default.QuoteTick)!;
        var bar = JsonSerializer.Deserialize(Lines("streams/bars").First(), TvJsonContext.Default.BarTick)!;
        var all = JsonSerializer.Deserialize(Lines("streams/all").First(), TvJsonContext.Default.AllPanesTick)!;

        Assert.Equal("CME_MINI:ES1!", quote.Symbol);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791579300), quote.Time);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1791666028258), quote.Timestamp);
        Assert.Equal("quote", quote.Stream);
        Assert.Equal(299, bar.BarIndex);
        Assert.Equal("5", bar.Resolution);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791579300), bar.BarTime);
        Assert.Equal("all-panes", all.Stream);
        Assert.Equal(1, all.PaneCount);
        Assert.Equal(7866, Assert.Single(all.Panes).Close);
        Assert.Null(all.Panes[0].Error);
    }

    [Fact]
    public void Ohlcv_feed_tick_uses_observed_at_instead_of_ts()
    {
        var tick = JsonSerializer.Deserialize(Lines("streams/ohlcv").First(), TvJsonContext.Default.OhlcvFeedTick)!;

        Assert.Equal(0, tick.PaneIndex);
        Assert.Equal("5", tick.Timeframe);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1791666070729), tick.ObservedAt);
        Assert.Equal(299, tick.BarIndex);
    }

    [Fact]
    public void Study_values_stream_carries_numbers_and_tolerates_null()
    {
        var tick = JsonSerializer.Deserialize(Lines("streams/values").First(), TvJsonContext.Default.StudyValuesTick)!;
        var withNull = JsonSerializer.Deserialize(
            """{"symbol":"X","study_count":1,"studies":[{"name":"S","values":{"a":1.5,"b":null}}],"_ts":1,"_stream":"values"}""",
            TvJsonContext.Default.StudyValuesTick)!;

        Assert.Equal(2, tick.StudyCount);
        Assert.Equal(400, tick.Studies[0].Values["_end"]);
        Assert.Null(withNull.Studies[0].Values["b"]);
        Assert.Equal(1.5, withNull.Studies[0].Values["a"]);
    }

    [Fact]
    public void Pine_stream_shapes_from_the_contract_parse()
    {
        var lines = JsonSerializer.Deserialize(
            """{"symbol":"X","study_count":1,"studies":[{"study":"S/R","levels":[7900.5,7800]}],"_ts":1,"_stream":"lines"}""",
            TvJsonContext.Default.PineLinesTick)!;
        var labels = JsonSerializer.Deserialize(
            """{"symbol":"X","study_count":1,"studies":[{"study":"L","labels":[{"text":"PDH","price":7900},{"text":"n","price":null}]}],"_ts":1,"_stream":"labels"}""",
            TvJsonContext.Default.PineLabelsTick)!;
        var tables = JsonSerializer.Deserialize(
            """{"symbol":"X","study_count":1,"studies":[{"study":"T","tables":[{"rows":[["Mode","D1"],["Regime","NEUTRAL"]]}]}],"_ts":1,"_stream":"tables"}""",
            TvJsonContext.Default.PineTablesTick)!;
        var panes = JsonSerializer.Deserialize(
            """{"layout":"2h","pane_count":2,"panes":[{"index":0,"symbol":"A","resolution":"5","time":1,"open":1,"high":2,"low":0.5,"close":1.5,"volume":10},{"index":1,"symbol":"B","error":"no bars"}],"_ts":1,"_stream":"all-panes"}""",
            TvJsonContext.Default.AllPanesTick)!;

        Assert.Equal([7900.5, 7800], lines.Studies[0].Levels);
        Assert.Null(labels.Studies[0].Labels[1].Price);
        Assert.Equal("NEUTRAL", tables.Studies[0].Tables[0].Rows[1][1]);
        Assert.Equal("no bars", panes.Panes[1].Error);
        Assert.Null(panes.Panes[1].Close);
        Assert.All(new object[] { lines, labels, tables, panes }, t => Assert.Empty(Fixtures.FindUnmappedFields(t)));
    }

    // --- FeedSpec -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("CME_MINI:ES1!@1", "CME_MINI:ES1!", "1")]
    [InlineData("NASDAQ:AAPL@15", "NASDAQ:AAPL", "15")]
    [InlineData("A@B@5", "A@B", "5")]
    [InlineData("BTC:USD@1D", "BTC:USD", "1D")]
    public void FeedSpec_parse_splits_at_the_last_at_sign(string text, string symbol, string interval)
    {
        var spec = FeedSpec.Parse(text);

        Assert.Equal((symbol, interval), (spec.Symbol, spec.Interval));
        Assert.Equal(text, spec.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("NASDAQ:AAPL")]
    [InlineData("@5")]
    [InlineData("AAPL@")]
    [InlineData(" @5")]
    public void FeedSpec_parse_rejects_malformed_text(string text)
        => Assert.Throws<FormatException>(() => FeedSpec.Parse(text));

    [Fact]
    public void FeedSpec_constructors_validate_and_canonicalize()
    {
        Assert.Equal("NASDAQ:AAPL@240", new FeedSpec("NASDAQ:AAPL", Timeframe.Hours(4)).ToString());
        Assert.Equal("X@1D", new FeedSpec("X", "1D").ToString());
        Assert.Equal(Timeframe.Days(1), new FeedSpec("X", "1D").Timeframe);
        Assert.Throws<ArgumentException>(() => new FeedSpec(" ", Timeframe.Minutes(1)));
        Assert.Throws<ArgumentException>(() => new FeedSpec("X", default(Timeframe)));
        Assert.Throws<ArgumentException>(() => new FeedSpec("X", " "));
        Assert.Equal(new FeedSpec("X", "5"), new FeedSpec("X", Timeframe.Minutes(5)));
    }

    // --- StreamApi ----------------------------------------------------------------------------------------

    private static async Task<List<T>> Collect<T>(IAsyncEnumerable<T> source, int max = int.MaxValue)
    {
        var items = new List<T>();
        await foreach (var item in source.WithCancellation(Ct))
        {
            items.Add(item);
            if (items.Count >= max)
            {
                break;
            }
        }

        return items;
    }

    [Fact]
    public async Task Quotes_yields_typed_ticks_in_order_and_builds_the_command()
    {
        var transport = new FakeTransport("") { StreamLines = [.. Lines("streams/quote"), .. Lines("streams/quote")] };
        using var client = transport.CreateClient();

        var ticks = await Collect(client.Streams.QuotesAsync(TimeSpan.FromMilliseconds(250), Ct));

        Assert.Equal(2, ticks.Count);
        Assert.Equal(["stream", "quote", "--interval=250"], transport.LastArgs);
        Assert.Null(transport.Commands[0].Timeout); // streams are not subject to the command timeout
    }

    [Fact]
    public async Task Streams_build_arguments_with_filters_and_default_intervals()
    {
        var transport = new FakeTransport("") { StreamLines = [] };
        using var client = transport.CreateClient();

        await Collect(client.Streams.QuotesAsync(cancellationToken: Ct));
        await Collect(client.Streams.BarsAsync(TimeSpan.FromSeconds(1), Ct));
        await Collect(client.Streams.StudyValuesAsync(cancellationToken: Ct));
        await Collect(client.Streams.PineLinesAsync("Profiler", TimeSpan.FromSeconds(2), Ct));
        await Collect(client.Streams.PineLabelsAsync(cancellationToken: Ct));
        await Collect(client.Streams.PineTablesAsync("-Stats", cancellationToken: Ct));
        await Collect(client.Streams.AllPanesAsync(TimeSpan.FromMilliseconds(749.6), Ct));

        Assert.Equal(
            [
                ["stream", "quote"], ["stream", "bars", "--interval=1000"], ["stream", "values"],
                ["stream", "lines", "--filter=Profiler", "--interval=2000"], ["stream", "labels"],
                ["stream", "tables", "--filter=-Stats"], ["stream", "all", "--interval=750"],
            ],
            transport.Commands.Select(c => c.Args.ToArray()));
    }

    [Fact]
    public async Task Ohlcv_sends_each_feed_as_one_argument()
    {
        var transport = new FakeTransport("") { StreamLines = Lines("streams/ohlcv").ToArray() };
        using var client = transport.CreateClient();
        FeedSpec[] feeds = [new("CME_MINI:ES1!", Timeframe.Minutes(1)), FeedSpec.Parse("NASDAQ:AAPL@15")];

        var ticks = await Collect(client.Streams.OhlcvAsync(feeds, TimeSpan.FromMilliseconds(100), Ct));

        Assert.Equal(["stream", "ohlcv", "--interval=100", "CME_MINI:ES1!@1", "NASDAQ:AAPL@15"], transport.LastArgs);
        Assert.Single(ticks);
    }

    [Fact]
    public void Arguments_are_validated_when_the_method_is_called_not_when_enumerated()
    {
        var transport = new FakeTransport("");
        using var client = transport.CreateClient();
        var feed = new FeedSpec("A", Timeframe.Minutes(1));

        Assert.Throws<ArgumentOutOfRangeException>(() => client.Streams.QuotesAsync(TimeSpan.Zero, Ct));
        Assert.Throws<ArgumentOutOfRangeException>(() => client.Streams.BarsAsync(TimeSpan.FromMilliseconds(0.4), Ct));
        Assert.Throws<ArgumentOutOfRangeException>(() => client.Streams.AllPanesAsync(TimeSpan.FromDays(40), Ct));
        Assert.Throws<ArgumentException>(() => client.Streams.OhlcvAsync([], cancellationToken: Ct));
        Assert.Throws<ArgumentException>(() => client.Streams.OhlcvAsync([feed, new FeedSpec("A", "1")], cancellationToken: Ct));
        Assert.Throws<ArgumentOutOfRangeException>(() => client.Streams.OhlcvAsync([feed], TimeSpan.FromMilliseconds(99), Ct));
        Assert.Throws<ArgumentNullException>(() => client.Streams.OhlcvAsync(null!, cancellationToken: Ct));
        Assert.Empty(transport.Commands);
    }

    [Fact]
    public async Task Nothing_runs_until_the_stream_is_enumerated()
    {
        var transport = new FakeTransport("");
        using var client = transport.CreateClient();

        var stream = client.Streams.QuotesAsync(cancellationToken: Ct);

        Assert.Empty(transport.Commands);
        await Collect(stream);
        Assert.Single(transport.Commands);
    }

    [Fact]
    public async Task A_malformed_line_raises_a_response_exception_with_the_line()
    {
        var transport = new FakeTransport("") { StreamLines = [.. Lines("streams/quote"), "{not json"] };
        using var client = transport.CreateClient();
        var received = new List<QuoteTick>();

        var ex = await Assert.ThrowsAsync<TvResponseException>(async () =>
        {
            await foreach (var tick in client.Streams.QuotesAsync(cancellationToken: Ct))
            {
                received.Add(tick);
            }
        });

        Assert.Single(received);
        Assert.Equal("{not json", ex.StdOut);
    }

    [Fact]
    public async Task A_json_null_line_is_an_error()
    {
        var transport = new FakeTransport("") { StreamLines = ["null"] };
        using var client = transport.CreateClient();

        await Assert.ThrowsAsync<TvResponseException>(() => Collect(client.Streams.BarsAsync(cancellationToken: Ct)));
    }

    [Fact]
    public async Task Cancelling_the_token_stops_the_stream()
    {
        var transport = new FakeTransport("") { StreamLines = Enumerable.Repeat(Lines("streams/quote").First(), 100).ToArray() };
        using var client = transport.CreateClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var seen = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var unused in client.Streams.QuotesAsync(cancellationToken: cts.Token))
            {
                if (++seen == 3)
                {
                    await cts.CancelAsync();
                }
            }
        });

        Assert.True(seen is >= 3 and < 100);
    }

    [Fact]
    public async Task Transport_failures_propagate_as_typed_exceptions()
    {
        var transport = new ThrowingStreamTransport(new TvConnectionException("CDP gone"));
        using var client = new TradingViewClient(transport);

        await Assert.ThrowsAsync<TvConnectionException>(() => Collect(client.Streams.QuotesAsync(cancellationToken: Ct)));
    }

    private sealed class ThrowingStreamTransport(Exception error) : ITvTransport
    {
        public Task<TvRawResult> ExecuteAsync(TvCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async IAsyncEnumerable<string> StreamLinesAsync(TvCommand command, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw error;
#pragma warning disable CS0162 // unreachable code is what makes this an iterator
            yield break;
#pragma warning restore CS0162
        }
    }
}
