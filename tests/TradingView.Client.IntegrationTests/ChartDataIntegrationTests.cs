namespace TradingView.Client.IntegrationTests;

/// <summary>Read-only checks against the live chart. They never change symbol, timeframe or studies.</summary>
public class ChartDataIntegrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<TradingViewClient> Client() => Live.CreateClientOrSkipAsync(Ct);

    [Fact]
    public async Task Chart_state_matches_symbol_and_timeframe_reads()
    {
        using var tv = await Client();

        var state = await tv.Chart.GetStateAsync(Ct);
        var symbol = await tv.Chart.GetSymbolAsync(Ct);
        var timeframe = await tv.Chart.GetTimeframeAsync(Ct);

        Assert.False(string.IsNullOrEmpty(state.Symbol));
        Assert.Equal(state.Symbol, symbol.Symbol);
        Assert.Equal(state.Resolution, timeframe.Resolution);
        Assert.NotNull(state.Timeframe); // the chart resolution is always a valid Timeframe
        Assert.Empty(state.AdditionalData ?? []);
    }

    [Fact]
    public async Task Chart_type_reports_a_raw_id_even_for_unnamed_styles()
    {
        using var tv = await Client();

        var state = await tv.Chart.GetStateAsync(Ct);
        var type = await tv.Chart.GetChartTypeAsync(Ct);

        Assert.Equal(state.ChartTypeId, type.TypeNum);
        Assert.False(string.IsNullOrEmpty(type.ChartTypeName));
    }

    [Fact]
    public async Task Symbol_info_describes_the_current_symbol()
    {
        using var tv = await Client();

        var info = await tv.Chart.GetSymbolInfoAsync(Ct);

        Assert.False(string.IsNullOrEmpty(info.FullName));
        Assert.False(string.IsNullOrEmpty(info.Exchange));
    }

    [Fact]
    public async Task Search_works_despite_the_upstream_node_shutdown_crash()
    {
        using var tv = await Client();

        var result = await tv.Chart.SearchSymbolsAsync("AAPL", Ct);

        Assert.Contains(result.Results, r => r.FullName == "NASDAQ:AAPL");
    }

    [Fact]
    public async Task Visible_range_is_ordered()
    {
        using var tv = await Client();

        var range = await tv.Chart.GetVisibleRangeAsync(Ct);

        Assert.True(range.VisibleRange.To > range.VisibleRange.From);
        Assert.True(range.VisibleRange.From > DateTimeOffset.UnixEpoch.AddYears(10));
    }

    [Fact]
    public async Task Quote_has_a_price_for_the_active_symbol()
    {
        using var tv = await Client();
        var symbol = await tv.Chart.GetSymbolAsync(Ct);

        var quote = await tv.Data.GetQuoteAsync(cancellationToken: Ct);

        Assert.Equal(symbol.Symbol, quote.Symbol);
        Assert.NotNull(quote.Last);
        Assert.True(quote.Last > 0);
    }

    [Fact]
    public async Task Ohlcv_returns_ordered_bars_and_a_consistent_summary()
    {
        using var tv = await Client();

        var ohlcv = await tv.Data.GetOhlcvAsync(30, Ct);
        var summary = await tv.Data.GetOhlcvSummaryAsync(30, Ct);

        Assert.Equal(ohlcv.BarCount, ohlcv.Bars.Count);
        Assert.InRange(ohlcv.Bars.Count, 1, 30);
        Assert.All(ohlcv.Bars, b => Assert.True(b.High >= b.Low && b.High >= b.Open && b.High >= b.Close));
        Assert.True(ohlcv.Bars.Zip(ohlcv.Bars.Skip(1)).All(p => p.First.Time < p.Second.Time));
        Assert.Equal(ohlcv.BarCount, summary.BarCount);
        Assert.Equal(5, summary.Last5Bars.Count);
        Assert.NotNull(summary.ChangePercentValue);
    }

    [Fact]
    public async Task Study_values_cover_the_visible_studies()
    {
        using var tv = await Client();

        var values = await tv.Data.GetStudyValuesAsync(Ct);

        Assert.Equal(values.StudyCount, values.Studies.Count);
        Assert.All(values.Studies, s => Assert.False(string.IsNullOrEmpty(s.Name)));
    }

    [Fact]
    public async Task Pine_drawing_readers_return_well_formed_results()
    {
        using var tv = await Client();

        var lines = await tv.Data.GetPineLinesAsync(cancellationToken: Ct);
        var labels = await tv.Data.GetPineLabelsAsync(maxLabels: 3, cancellationToken: Ct);
        var tables = await tv.Data.GetPineTablesAsync(cancellationToken: Ct);
        var boxes = await tv.Data.GetPineBoxesAsync(cancellationToken: Ct);

        Assert.Equal(lines.StudyCount, lines.Studies.Count);
        Assert.Equal(labels.StudyCount, labels.Studies.Count);
        Assert.Equal(tables.StudyCount, tables.Studies.Count);
        Assert.Equal(boxes.StudyCount, boxes.Studies.Count);
        Assert.All(labels.Studies, s => Assert.True(s.Labels.Count <= 3));
    }

    [Fact]
    public async Task Indicator_details_resolve_for_the_first_study()
    {
        using var tv = await Client();
        var state = await tv.Chart.GetStateAsync(Ct);
        if (state.Studies.Count == 0)
        {
            Assert.Skip("The chart has no studies.");
        }

        var study = state.Studies[0];
        var info = await tv.Data.GetIndicatorAsync(study.Id, Ct);

        Assert.Equal(study.Id, info.EntityId);
    }

    [Fact]
    public async Task Unknown_indicator_id_is_a_command_error()
    {
        using var tv = await Client();

        await Assert.ThrowsAsync<TvCommandException>(() => tv.Data.GetIndicatorAsync("nope00", Ct));
    }

    [Fact]
    public async Task Strategy_data_is_either_present_or_reported_as_operation_failure()
    {
        using var tv = await Client();
        // `tv data strategy` opens the Strategy Tester in the bottom panel as a side effect.
        var bottomWasOpen = (await tv.Health.GetUiStateAsync(Ct)).BottomPanel?.Open == true;
        try
        {
            var strategy = await tv.Data.GetStrategyResultsAsync(Ct);
            Assert.True(strategy.MetricCount > 0);
        }
        catch (TvOperationFailedException ex)
        {
            Assert.IsType<StrategyResultsResult>(ex.Result);
            Assert.False(string.IsNullOrEmpty(ex.Message));
        }
        finally
        {
            if (!bottomWasOpen && (await tv.Health.GetUiStateAsync(Ct)).BottomPanel?.Open == true)
            {
                await tv.Ui.ClickAsync(UiSelector.AriaLabel, "Collapse panel", Ct);
            }
        }
    }

    [Fact]
    public async Task Invalid_ohlcv_count_never_reaches_the_cli()
    {
        using var tv = await Client();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tv.Data.GetOhlcvAsync(501, Ct));
    }
}
