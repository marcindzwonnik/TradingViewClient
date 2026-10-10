namespace TradingView.Client.IntegrationTests;

public class PineIndicatorReplayIntegrationTests
{
    private const string ValidScript = "//@version=6\nindicator(\"TV Client Integration\", overlay = true)\nplot(close)\n";
    private const string BrokenScript = "//@version=6\nindicator(\"Broken\")\nplot(undefined_variable)\n";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<TradingViewClient> Client() => Live.CreateClientOrSkipAsync(Ct);

    // --- read-only ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Pine_analyze_runs_offline_checks()
    {
        using var tv = await Client();

        var clean = await tv.Pine.AnalyzeAsync(ValidScript, Ct);
        var issues = await tv.Pine.AnalyzeAsync("//@version=6\nindicator(\"x\")\na = array.new_float(3)\nx = array.get(a, 5)\n", Ct);

        Assert.Equal(0, clean.IssueCount);
        Assert.Contains(issues.Diagnostics, d => d.Severity == PineDiagnosticSeverity.Error && d.Line == 4);
    }

    [Fact]
    public async Task Pine_check_reports_compile_status_without_throwing()
    {
        using var tv = await Client();

        var ok = await tv.Pine.CheckAsync(ValidScript, Ct);
        var broken = await tv.Pine.CheckAsync(BrokenScript, Ct);

        Assert.True(ok.Compiled);
        Assert.False(broken.Compiled);
        Assert.Equal(1, broken.ErrorCount);
        Assert.Equal(3, broken.Errors![0].Line);
    }

    [Fact]
    public async Task Pine_check_handles_unicode_and_special_characters()
    {
        using var tv = await Client();
        var source = "//@version=6\nindicator(\"Zażółć gęślą jaźń ✓ & ^ % !\")\nplot(close)\n";

        var result = await tv.Pine.CheckAsync(source, Ct);

        Assert.True(result.Compiled);
    }

    [Fact]
    public async Task Pine_list_returns_saved_scripts()
    {
        using var tv = await Client();

        var list = await tv.Pine.ListAsync(Ct);

        Assert.Equal(list.Count, list.Scripts.Count);
        Assert.All(list.Scripts, s => Assert.False(string.IsNullOrEmpty(s.Name)));
    }

    [Fact]
    public async Task Replay_status_is_readable_when_stopped()
    {
        using var tv = await Client();
        var status = await tv.Replay.GetStatusAsync(Ct);
        if (status.IsReplayStarted)
        {
            Assert.Skip("Replay is already running on the chart.");
        }

        Assert.True(status.IsReplayAvailable);
        Assert.Null(status.CurrentDate);
    }

    [Fact]
    public async Task Replay_commands_fail_cleanly_when_replay_is_not_running()
    {
        using var tv = await Client();
        if ((await tv.Replay.GetStatusAsync(Ct)).IsReplayStarted)
        {
            Assert.Skip("Replay is already running on the chart.");
        }

        await Assert.ThrowsAsync<TvCommandException>(() => tv.Replay.StepAsync(Ct));
        await Assert.ThrowsAsync<TvCommandException>(() => tv.Replay.TradeAsync(ReplayTradeAction.Buy, Ct));
        Assert.False((await tv.Replay.StopAsync(Ct)).WasRunning);
    }

    [Fact]
    public async Task Invalid_autoplay_speed_never_reaches_the_cli()
    {
        using var tv = await Client();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tv.Replay.ToggleAutoplayAsync((ReplaySpeed)150, Ct));
    }

    // --- mutating (TV_INTEGRATION_MUTATING=1); each test restores what it changed ----------------------------

    [Fact]
    public async Task Indicator_lifecycle_add_configure_hide_remove()
    {
        Live.SkipUnlessMutating();
        using var tv = await Client();
        var before = (await tv.Chart.GetStateAsync(Ct)).Studies.Select(s => s.Id).ToHashSet();
        try
        {
            var added = await tv.Indicators.AddAsync(
                "Relative Strength Index",
                new Dictionary<string, object?> { ["length"] = 7, ["no_such_input"] = 1 },
                Ct);

            Assert.False(string.IsNullOrEmpty(added.EntityId));
            Assert.Equal(7, added.Inputs!.Applied!["length"].GetInt32());
            Assert.Equal(["no_such_input"], added.Inputs.UnknownInputs);
            Assert.Contains((await tv.Chart.GetStateAsync(Ct)).Studies, s => s.Id == added.EntityId);

            var set = await tv.Indicators.SetInputsAsync(added.EntityId!, new Dictionary<string, object?> { ["length"] = 21 }, Ct);
            Assert.Equal(21, set.UpdatedInputs["length"].GetInt32());
            var info = await tv.Indicators.GetAsync(added.EntityId!, Ct);
            Assert.Contains(info.Inputs!, i => i.Id == "length" && i.Value.GetInt32() == 21);

            Assert.False((await tv.Indicators.SetVisibilityAsync(added.EntityId!, false, Ct)).Visible);
            Assert.True((await tv.Indicators.SetVisibilityAsync(added.EntityId!, true, Ct)).Visible);

            await tv.Indicators.RemoveAsync(added.EntityId!, Ct);
            Assert.DoesNotContain((await tv.Chart.GetStateAsync(Ct)).Studies, s => s.Id == added.EntityId);
        }
        finally
        {
            await RemoveNewStudiesAsync(tv, before);
        }
    }

    [Fact]
    public async Task Unknown_indicator_name_throws_operation_failed()
    {
        Live.SkipUnlessMutating();
        using var tv = await Client();
        var before = (await tv.Chart.GetStateAsync(Ct)).Studies.Select(s => s.Id).ToHashSet();
        try
        {
            await Assert.ThrowsAsync<TvOperationFailedException>(() => tv.Indicators.AddAsync("ZZZ Nonexistent Indicator 12345", cancellationToken: Ct));
        }
        finally
        {
            await RemoveNewStudiesAsync(tv, before);
        }
    }

    [Fact]
    public async Task Chart_symbol_timeframe_round_trip_is_restored()
    {
        Live.SkipUnlessMutating();
        using var tv = await Client();
        var original = await tv.Chart.GetStateAsync(Ct);
        var originalRange = await tv.Chart.GetVisibleRangeAsync(Ct);
        try
        {
            await tv.Chart.SetSymbolAsync("NASDAQ:AAPL", Ct);
            await tv.Chart.SetTimeframeAsync(Timeframe.Hours(1), Ct);

            var changed = await tv.Chart.GetStateAsync(Ct);
            Assert.Contains("AAPL", changed.Symbol);
            Assert.Equal(Timeframe.Hours(1), changed.Timeframe);
        }
        finally
        {
            await tv.Chart.SetSymbolAsync(original.Symbol, Ct);
            await tv.Chart.SetTimeframeAsync(original.Timeframe ?? Timeframe.Minutes(5), Ct);
            await tv.Chart.SetVisibleRangeAsync(originalRange.VisibleRange.From, originalRange.VisibleRange.To, Ct);
        }

        var restored = await tv.Chart.GetStateAsync(Ct);
        Assert.Equal(original.Symbol, restored.Symbol);
        Assert.Equal(original.Resolution, restored.Resolution);
    }

    [Fact]
    public async Task Pine_editor_source_round_trip()
    {
        Live.SkipUnlessMutating();
        using var tv = await Client();

        var set = await tv.Pine.SetSourceAsync(ValidScript, Ct);
        var get = await tv.Pine.GetSourceAsync(Ct);

        Assert.Equal(4, set.LinesSet);
        Assert.Equal(ValidScript.TrimEnd('\n'), get.Source.TrimEnd('\n'));
    }

    [Fact]
    public async Task Replay_start_step_trade_stop_is_restored()
    {
        Live.SkipUnlessMutating();
        using var tv = await Client();
        if ((await tv.Replay.GetStatusAsync(Ct)).IsReplayStarted)
        {
            Assert.Skip("Replay is already running on the chart.");
        }

        try
        {
            var start = await tv.Replay.StartAsync(cancellationToken: Ct);
            Assert.True(start.ReplayStarted);
            Assert.NotNull(start.CurrentDate);

            var step = await tv.Replay.StepAsync(Ct);
            Assert.True(step.CurrentDate > start.CurrentDate);

            Assert.Equal("buy", (await tv.Replay.TradeAsync(ReplayTradeAction.Buy, Ct)).Action);
            Assert.Equal("close", (await tv.Replay.TradeAsync(ReplayTradeAction.Close, Ct)).Action);

            var on = await tv.Replay.ToggleAutoplayAsync(ReplaySpeed.Ms1000, Ct);
            Assert.True(on.AutoplayActive);
            Assert.Equal(1000, on.DelayMs);
            Assert.False((await tv.Replay.ToggleAutoplayAsync(cancellationToken: Ct)).AutoplayActive);

            var status = await tv.Replay.GetStatusAsync(Ct);
            Assert.True(status.IsReplayStarted);
        }
        finally
        {
            await tv.Replay.StopAsync(Ct);
        }

        Assert.False((await tv.Replay.GetStatusAsync(Ct)).IsReplayStarted);
    }

    private static async Task RemoveNewStudiesAsync(TradingViewClient tv, HashSet<string> before)
    {
        foreach (var study in (await tv.Chart.GetStateAsync(Ct)).Studies.Where(s => !before.Contains(s.Id)))
        {
            await tv.Indicators.RemoveAsync(study.Id, Ct);
        }
    }
}
