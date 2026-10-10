using System.Text.Json;
using TradingView.Client.Tests.Support;

namespace TradingView.Client.Tests;

/// <summary>Value-level checks on captured fixtures plus shapes that only the contract documents (verbose variants).</summary>
public class ChartDataModelTests
{
    private static T Fixture<T>(string name) where T : TvResult => (T)Fixtures.Deserialize(name);

    [Fact]
    public void Chart_state_exposes_studies_and_unnamed_chart_style()
    {
        var state = Fixture<ChartStateResult>("chart/state");

        Assert.Equal("CME_MINI:ES1!", state.Symbol);
        Assert.Equal("5", state.Resolution);
        Assert.Equal(19, state.ChartTypeId);
        Assert.Null(state.ChartType); // 19 is not one of the ten settable styles
        Assert.Equal(Timeframe.Minutes(5), state.Timeframe);
        Assert.NotEmpty(state.Studies);
        Assert.All(state.Studies, s => Assert.False(string.IsNullOrEmpty(s.Id)));
    }

    [Fact]
    public void Chart_type_accepts_number_in_the_name_field()
    {
        var type = Fixture<ChartTypeResult>("chart/type");

        Assert.Equal("19", type.ChartTypeName);
        Assert.Equal(19, type.TypeNum);
        Assert.Null(type.Type);
    }

    [Fact]
    public void Chart_type_accepts_named_style()
    {
        var type = JsonSerializer.Deserialize("""{"success":true,"chart_type":"HeikinAshi","type_num":8}""", TvJsonContext.Default.ChartTypeResult)!;

        Assert.Equal("HeikinAshi", type.ChartTypeName);
        Assert.Equal(ChartType.HeikinAshi, type.Type);
    }

    [Fact]
    public void Visible_range_maps_unix_seconds_to_timestamps()
    {
        var range = Fixture<VisibleRangeResult>("chart/range");

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791560400), range.VisibleRange.From);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791758700), range.VisibleRange.To);
        Assert.True(range.BarsRange.To > range.BarsRange.From);
    }

    [Fact]
    public void Set_visible_range_and_scroll_shapes_parse()
    {
        var set = JsonSerializer.Deserialize(
            """{"success":true,"requested":{"from":1700000000,"to":1700100000},"actual":{"from":1700000300.5,"to":1700099900}}""",
            TvJsonContext.Default.SetVisibleRangeResult)!;
        var scroll = JsonSerializer.Deserialize(
            """{"success":true,"date":"2025-01-15","centered_on":1736899200,"resolution":"D","window":{"from":1734739200,"to":1739059200}}""",
            TvJsonContext.Default.ScrollResult)!;

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), set.Requested.From);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000300), set.Actual.From);
        Assert.Null(set.Actual.Error);
        Assert.Equal("2025-01-15", scroll.Date);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1736899200), scroll.CenteredOn);
        Assert.Equal(1739059200, scroll.Window.To.ToUnixTimeSeconds());
    }

    [Fact]
    public void Set_symbol_and_timeframe_results_parse()
    {
        var symbol = JsonSerializer.Deserialize("""{"success":true,"symbol":"NASDAQ:AAPL","chart_ready":false}""", TvJsonContext.Default.SetSymbolResult)!;
        var tf = JsonSerializer.Deserialize("""{"success":true,"timeframe":"60","chart_ready":true}""", TvJsonContext.Default.SetTimeframeResult)!;

        Assert.False(symbol.ChartReady);
        Assert.Equal("60", tf.Timeframe);
        Assert.True(tf.ChartReady);
    }

    [Fact]
    public void Symbol_search_lists_full_names()
    {
        var search = Fixture<SymbolSearchResult>("chart/search");

        Assert.Equal("AAPL", search.Query);
        Assert.Equal(search.Count, search.Results.Count);
        Assert.Contains(search.Results, r => r.FullName == "NASDAQ:AAPL" && r.Exchange == "NASDAQ");
    }

    [Fact]
    public void Symbol_info_maps_snake_case_fields()
    {
        var info = Fixture<SymbolInfoResult>("chart/info");

        Assert.Equal("CME_MINI:ES1!", info.FullName);
        Assert.Equal("futures", info.Type);
        Assert.Contains("continuous", info.Typespecs!);
        Assert.Equal(19, info.ChartTypeId);
    }

    [Fact]
    public void Ohlcv_bars_have_timestamps_and_prices()
    {
        var ohlcv = Fixture<OhlcvResult>("data/ohlcv");

        Assert.Equal(5, ohlcv.BarCount);
        Assert.Equal(5, ohlcv.Bars.Count);
        Assert.True(ohlcv.TotalAvailable >= ohlcv.BarCount);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791578100), ohlcv.Bars[0].Time);
        Assert.Equal(7862.5, ohlcv.Bars[0].Close);
        Assert.All(ohlcv.Bars, b => Assert.True(b.High >= b.Low));
        Assert.True(ohlcv.Bars.Zip(ohlcv.Bars.Skip(1)).All(p => p.First.Time < p.Second.Time));
    }

    [Fact]
    public void Ohlcv_summary_parses_percent_string()
    {
        var summary = Fixture<OhlcvSummaryResult>("data/ohlcv-summary");

        Assert.Equal(20, summary.BarCount);
        Assert.Equal("-0.02%", summary.ChangePct);
        Assert.Equal(-0.02, summary.ChangePercentValue);
        Assert.Equal(5, summary.Last5Bars.Count);
        Assert.True(summary.Period.To > summary.Period.From);
    }

    [Fact]
    public void Quote_maps_optional_fields()
    {
        var quote = Fixture<QuoteResult>("data/quote");

        Assert.Equal("CME_MINI:ES1!", quote.Symbol);
        Assert.Equal(quote.Close, quote.Last);
        Assert.NotNull(quote.Time);
        Assert.Null(quote.Bid);
        Assert.Null(quote.Ask);
    }

    [Fact]
    public void Study_values_stay_formatted_strings()
    {
        var values = Fixture<StudyValuesResult>("data/values");

        Assert.Equal(values.StudyCount, values.Studies.Count);
        var ema = Assert.Single(values.Studies, s => s.Name == "Moving Average Exponential");
        Assert.Equal("7,863.35", ema.Values["EMA"]);
        Assert.True(DataWindowNumber.TryParse(ema.Values["EMA"], out var number));
        Assert.Equal(7863.35, number);
        Assert.NotNull(ema.Inputs);
    }

    [Fact]
    public void Labels_table_and_indicator_fixtures_map()
    {
        var labels = Fixture<PineLabelsResult>("data/labels");
        var verbose = Fixture<PineLabelsResult>("data/labels-verbose");
        var tables = Fixture<PineTablesResult>("data/tables");
        var indicator = Fixture<IndicatorInfoResult>("data/indicator");

        var study = Assert.Single(labels.Studies);
        Assert.Equal(15, study.TotalLabels);
        Assert.Equal(5, study.Labels.Count);
        Assert.Null(study.Labels[0].Id); // only present with --verbose
        var verboseLabel = verbose.Studies[0].Labels[0];
        Assert.NotNull(verboseLabel.Id);
        Assert.NotNull(verboseLabel.TextColor);
        Assert.Contains("Mode | D1 Strict", Assert.Single(Assert.Single(tables.Studies).Tables).Rows);
        Assert.False(indicator.Visible);
        Assert.Contains(indicator.Inputs!, i => i.Id == "in_1" && i.Value.GetInt32() == 20);
    }

    [Theory]
    [InlineData("data/strategy")]
    [InlineData("data/trades")]
    [InlineData("data/equity")]
    public void Missing_strategy_responses_are_soft_failures_with_a_message(string fixture)
    {
        var result = Fixtures.Deserialize(fixture);

        Assert.False(result.Success);
        Assert.Contains("No strategy found", result.Error);
    }

    [Fact]
    public void Verbose_lines_and_boxes_shapes_from_the_contract_parse()
    {
        var lines = JsonSerializer.Deserialize(
            """{"success":true,"study_count":1,"studies":[{"name":"S/R","total_lines":2,"horizontal_levels":[7900.5,7800],"all_lines":[{"id":1,"y1":7900.5,"y2":7900.5,"x1":10,"x2":20,"horizontal":true,"style":"solid","width":2,"color":255}]}]}""",
            TvJsonContext.Default.PineLinesResult)!;
        var boxes = JsonSerializer.Deserialize(
            """{"success":true,"study_count":1,"studies":[{"name":"Zones","total_boxes":1,"zones":[{"high":7910,"low":7890}],"all_boxes":[{"id":5,"high":7910,"low":7890,"x1":1,"x2":2,"borderColor":3,"bgColor":4}]}]}""",
            TvJsonContext.Default.PineBoxesResult)!;

        Assert.Equal([7900.5, 7800], lines.Studies[0].HorizontalLevels);
        Assert.True(lines.Studies[0].AllLines![0].Horizontal);
        Assert.Equal(2, lines.Studies[0].AllLines![0].Width!.Value.GetInt32());
        Assert.Equal(7910, boxes.Studies[0].Zones[0].High);
        Assert.Equal(3, boxes.Studies[0].AllBoxes![0].BorderColor!.Value.GetInt32());
        Assert.Equal(4, boxes.Studies[0].AllBoxes![0].BgColor!.Value.GetInt32());
        Assert.Empty(Fixtures.FindUnmappedFields(lines));
        Assert.Empty(Fixtures.FindUnmappedFields(boxes));
    }

    [Fact]
    public void Strategy_trades_equity_and_depth_shapes_from_the_contract_parse()
    {
        var strategy = JsonSerializer.Deserialize(
            """{"success":true,"metric_count":3,"strategy":"MyStrat","currency":"USD","source":"internal_api","metrics":{"net_profit":1234.5,"total_trades":10,"sharpe_ratio":1.2,"open_pl":0}}""",
            TvJsonContext.Default.StrategyResultsResult)!;
        var trades = JsonSerializer.Deserialize(
            """{"success":true,"trade_count":1,"total_orders":2,"source":"internal_api","trades":[{"id":1,"type":"e","side":"buy","entry":true,"price":7800.25,"qty":2,"time_index":42}]}""",
            TvJsonContext.Default.TradesResult)!;
        var depth = JsonSerializer.Deserialize(
            """{"success":true,"bid_levels":1,"ask_levels":1,"spread":0.25,"bids":[{"price":7800,"size":5}],"asks":[{"price":7800.25,"size":3}]}""",
            TvJsonContext.Default.DepthResult)!;

        Assert.Equal(1234.5, strategy.Metrics.NetProfit);
        Assert.Equal(10, strategy.Metrics.TotalTrades);
        Assert.Equal("buy", trades.Trades[0].Side);
        Assert.Equal(7800.25, trades.Trades[0].Price);
        Assert.Equal(0.25, depth.Spread);
        Assert.Equal(3, depth.Asks[0].Size);
        Assert.Empty(Fixtures.FindUnmappedFields(strategy));
        Assert.Empty(Fixtures.FindUnmappedFields(trades));
        Assert.Empty(Fixtures.FindUnmappedFields(depth));
    }

    [Theory]
    [InlineData("7,863.35", 7863.35)]
    [InlineData("−0.32", -0.32)]
    [InlineData("-0.02%", -0.02)]
    [InlineData("1 234,5", 12345)] // ',' is treated as a thousands separator
    [InlineData("0", 0)]
    public void DataWindowNumber_parses_display_formats(string text, double expected)
    {
        Assert.True(DataWindowNumber.TryParse(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NaN")]
    [InlineData("n/a")]
    public void DataWindowNumber_rejects_non_numbers(string? text)
        => Assert.False(DataWindowNumber.TryParse(text, out _));
}
