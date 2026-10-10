using TradingView.Client.Tests.Support;

namespace TradingView.Client.Tests;

public class ChartDataApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A client whose transport answers every command with the fixture named after it.</summary>
    private static (TradingViewClient Client, FakeTransport Transport) Create(string fixture)
    {
        var transport = new FakeTransport(_ => new TvRawResult(0, Fixtures.Read(fixture + ".json"), string.Empty));
        return (transport.CreateClient(), transport);
    }

    public static TheoryData<string, string[], string> ChartCalls() => new()
    {
        { "chart/state", ["state"], "state" },
        { "chart/symbol", ["symbol"], "get-symbol" },
        { "chart/timeframe", ["timeframe"], "get-tf" },
        { "chart/type", ["type"], "get-type" },
        { "chart/info", ["info"], "info" },
        { "chart/range", ["range"], "get-range" },
    };

    [Theory]
    [MemberData(nameof(ChartCalls))]
    public async Task Chart_read_methods_send_the_expected_command(string fixture, string[] expectedArgs, string call)
    {
        var (client, transport) = Create(fixture);
        using var _ = client;

        Task task = call switch
        {
            "state" => client.Chart.GetStateAsync(Ct),
            "get-symbol" => client.Chart.GetSymbolAsync(Ct),
            "get-tf" => client.Chart.GetTimeframeAsync(Ct),
            "get-type" => client.Chart.GetChartTypeAsync(Ct),
            "info" => client.Chart.GetSymbolInfoAsync(Ct),
            "get-range" => client.Chart.GetVisibleRangeAsync(Ct),
            _ => throw new ArgumentOutOfRangeException(nameof(call)),
        };
        await task;

        Assert.Equal(expectedArgs, transport.LastArgs);
    }

    [Fact]
    public async Task SetSymbol_passes_the_symbol_untouched()
    {
        var transport = new FakeTransport("""{"success":true,"symbol":"CME_MINI:ES1!","chart_ready":true}""");
        using var client = transport.CreateClient();

        var result = await client.Chart.SetSymbolAsync("CME_MINI:ES1!", Ct);

        Assert.Equal(["symbol", "CME_MINI:ES1!"], transport.LastArgs);
        Assert.True(result.ChartReady);
    }

    [Fact]
    public async Task SetTimeframe_sends_the_canonical_form()
    {
        var transport = new FakeTransport("""{"success":true,"timeframe":"240","chart_ready":true}""");
        using var client = transport.CreateClient();

        await client.Chart.SetTimeframeAsync(Timeframe.Hours(4), Ct);
        await client.Chart.SetTimeframeAsync("1D", Ct);

        Assert.Equal([["timeframe", "240"], ["timeframe", "D"]], transport.Commands.Select(c => c.Args.ToArray()));
    }

    [Theory]
    [InlineData(ChartType.Candles, "Candles")]
    [InlineData(ChartType.HeikinAshi, "HeikinAshi")]
    [InlineData(ChartType.PointAndFigure, "PointAndFigure")]
    public async Task SetChartType_sends_the_case_sensitive_name(ChartType type, string name)
    {
        var transport = new FakeTransport($$"""{"success":true,"chart_type":"{{name}}","type_num":{{(int)type}}}""");
        using var client = transport.CreateClient();

        var result = await client.Chart.SetChartTypeAsync(type, Ct);

        Assert.Equal(["type", name], transport.LastArgs);
        Assert.Equal(type, result.Type);
    }

    [Fact]
    public async Task Invalid_chart_input_is_rejected_before_starting_the_process()
    {
        var transport = new FakeTransport("{}");
        using var client = transport.CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() => client.Chart.SetSymbolAsync(" ", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Chart.SetTimeframeAsync(default, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Chart.SetChartTypeAsync((ChartType)42, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Chart.SearchSymbolsAsync("", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Chart.SetVisibleRangeAsync(DateTimeOffset.UnixEpoch.AddDays(2), DateTimeOffset.UnixEpoch, Ct));

        Assert.Empty(transport.Commands);
    }

    [Fact]
    public async Task Search_keeps_multi_word_queries_as_one_argument()
    {
        var (client, transport) = Create("chart/search");
        using var _ = client;

        var result = await client.Chart.SearchSymbolsAsync("apple inc", Ct);

        Assert.Equal(["search", "apple inc"], transport.LastArgs);
        Assert.NotEmpty(result.Results);
    }

    [Fact]
    public async Task Range_and_scroll_use_unix_seconds()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, cmd.Args[0] == "range"
            ? """{"success":true,"requested":{"from":1,"to":2},"actual":{"from":1,"to":2}}"""
            : """{"success":true,"date":"x","centered_on":1,"resolution":"5","window":{"from":0,"to":2}}""", string.Empty));
        using var client = transport.CreateClient();
        var from = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);

        await client.Chart.SetVisibleRangeAsync(from, from.AddDays(1), Ct);
        await client.Chart.ScrollToDateAsync(from.ToOffset(TimeSpan.FromHours(5)), Ct);

        Assert.Equal(["range", "--from", "1736899200", "--to", "1736985600"], transport.Commands[0].Args);
        Assert.Equal(["scroll", "1736899200"], transport.Commands[1].Args);
    }

    [Fact]
    public async Task Quote_with_and_without_symbol()
    {
        var (client, transport) = Create("data/quote");
        using var _ = client;

        await client.Data.GetQuoteAsync(cancellationToken: Ct);
        await client.Data.GetQuoteAsync("NASDAQ:AAPL", Ct);
        await client.Data.GetQuoteAsync("  ", Ct);

        Assert.Equal([["quote"], ["quote", "NASDAQ:AAPL"], ["quote"]], transport.Commands.Select(c => c.Args.ToArray()));
    }

    [Fact]
    public async Task Ohlcv_methods_send_count_and_summary_flag()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, Fixtures.Read(cmd.Args.Contains("--summary") ? "data/ohlcv-summary.json" : "data/ohlcv.json"), string.Empty));
        using var client = transport.CreateClient();

        await client.Data.GetOhlcvAsync(cancellationToken: Ct);
        await client.Data.GetOhlcvAsync(500, Ct);
        await client.Data.GetOhlcvSummaryAsync(20, Ct);

        Assert.Equal(
            [["ohlcv", "-n", "100"], ["ohlcv", "-n", "500"], ["ohlcv", "-n", "20", "--summary"]],
            transport.Commands.Select(c => c.Args.ToArray()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    [InlineData(-1)]
    public async Task Ohlcv_count_is_validated(int count)
    {
        var transport = new FakeTransport("{}");
        using var client = transport.CreateClient();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Data.GetOhlcvAsync(count, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Data.GetOhlcvSummaryAsync(count, Ct));
        Assert.Empty(transport.Commands);
    }

    public static TheoryData<string, string[]> PineCalls() => new()
    {
        { "lines", ["data", "lines"] },
        { "lines-filter-verbose", ["data", "lines", "--filter", "Profiler", "--verbose"] },
        { "labels", ["data", "labels"] },
        { "labels-all", ["data", "labels", "--filter", "SMC", "--max", "5", "--verbose"] },
        { "tables", ["data", "tables"] },
        { "tables-filter", ["data", "tables", "--filter", "Stats"] },
        { "boxes", ["data", "boxes"] },
        { "boxes-verbose", ["data", "boxes", "--filter", "Zones", "--verbose"] },
    };

    [Theory]
    [MemberData(nameof(PineCalls))]
    public async Task Pine_data_methods_build_filter_max_and_verbose_flags(string call, string[] expected)
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, $$"""{"success":true,"study_count":0,"studies":[]}""", string.Empty));
        using var client = transport.CreateClient();

        Task task = call switch
        {
            "lines" => client.Data.GetPineLinesAsync(cancellationToken: Ct),
            "lines-filter-verbose" => client.Data.GetPineLinesAsync("Profiler", true, Ct),
            "labels" => client.Data.GetPineLabelsAsync(cancellationToken: Ct),
            "labels-all" => client.Data.GetPineLabelsAsync("SMC", 5, true, Ct),
            "tables" => client.Data.GetPineTablesAsync(cancellationToken: Ct),
            "tables-filter" => client.Data.GetPineTablesAsync("Stats", Ct),
            "boxes" => client.Data.GetPineBoxesAsync(cancellationToken: Ct),
            "boxes-verbose" => client.Data.GetPineBoxesAsync("Zones", true, Ct),
            _ => throw new ArgumentOutOfRangeException(nameof(call)),
        };
        await task;

        Assert.Equal(expected, transport.LastArgs);
    }

    [Fact]
    public async Task Study_values_and_indicator_use_their_commands()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, Fixtures.Read(cmd.Args[0] == "values" ? "data/values.json" : "data/indicator.json"), string.Empty));
        using var client = transport.CreateClient();

        await client.Data.GetStudyValuesAsync(Ct);
        var indicator = await client.Data.GetIndicatorAsync("e6NNI2", Ct);

        Assert.Equal([["values"], ["data", "indicator", "e6NNI2"]], transport.Commands.Select(c => c.Args.ToArray()));
        Assert.Equal("e6NNI2", indicator.EntityId);
        await Assert.ThrowsAsync<ArgumentException>(() => client.Data.GetIndicatorAsync("", Ct));
    }

    [Fact]
    public async Task Trades_max_is_validated_and_sent()
    {
        var transport = new FakeTransport("""{"success":true,"trade_count":0,"total_orders":0,"trades":[]}""");
        using var client = transport.CreateClient();

        await client.Data.GetTradesAsync(cancellationToken: Ct);
        await client.Data.GetTradesAsync(20, Ct);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Data.GetTradesAsync(21, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Data.GetTradesAsync(0, Ct));

        Assert.Equal([["data", "trades"], ["data", "trades", "-n", "20"]], transport.Commands.Select(c => c.Args.ToArray()));
    }

    [Theory]
    [InlineData("data/strategy")]
    [InlineData("data/trades")]
    [InlineData("data/equity")]
    public async Task Strategy_methods_throw_operation_failed_when_no_strategy_exists(string fixture)
    {
        var (client, _) = Create(fixture);
        using var _ = client;

        var ex = await Assert.ThrowsAsync<TvOperationFailedException>(() => fixture switch
        {
            "data/strategy" => client.Data.GetStrategyResultsAsync(Ct),
            "data/trades" => client.Data.GetTradesAsync(5, Ct),
            _ => client.Data.GetEquityAsync(Ct),
        });

        Assert.Contains("No strategy found", ex.Message);
    }

    [Fact]
    public async Task Strategy_failure_can_be_returned_instead_of_thrown()
    {
        var transport = new FakeTransport(_ => new TvRawResult(0, Fixtures.Read("data/strategy.json"), string.Empty));
        using var client = transport.CreateClient(new TvClientOptions { ThrowOnOperationFailure = false });

        var result = await client.Data.GetStrategyResultsAsync(Ct);

        Assert.False(result.Success);
        Assert.Equal(0, result.MetricCount);
    }

    [Fact]
    public async Task Depth_without_panel_is_a_command_exception_with_the_cli_message()
    {
        var transport = new FakeTransport("", exitCode: 1, stdErr: """{"success":false,"error":"DOM / Depth of Market panel not found."}""");
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvCommandException>(() => client.Data.GetDepthAsync(Ct));

        Assert.Equal("DOM / Depth of Market panel not found.", ex.Message);
    }
}
