using System.Text.Json;
using TradingView.Client.Tests.Support;

namespace TradingView.Client.Tests;

public class PineIndicatorReplayModelTests
{
    private static T Fixture<T>(string name) where T : TvResult => (T)Fixtures.Deserialize(name);

    // --- Pine ---------------------------------------------------------------------------------------------

    [Fact]
    public void Pine_get_and_set_report_sizes()
    {
        var get = Fixture<PineSourceResult>("pine/get");
        var set = Fixture<PineSetSourceResult>("pine/set");

        Assert.Contains("indicator(\"TV Client Test\"", get.Source);
        Assert.Equal(get.Source.Length, get.CharCount);
        Assert.Equal(5, get.LineCount);
        Assert.Equal(5, set.LinesSet);
    }

    [Fact]
    public void Pine_compile_success_has_no_errors_and_no_study_for_unsaved_scripts()
    {
        var compile = Fixture<PineCompileResult>("pine/compile");

        Assert.True(compile.Success);
        Assert.False(compile.HasErrors);
        Assert.Empty(compile.Errors);
        Assert.Equal("Pine Save", compile.ButtonClicked);
        Assert.False(compile.StudyAdded);
    }

    [Fact]
    public void Pine_compile_errors_are_data_not_exceptions()
    {
        var compile = Fixture<PineCompileResult>("pine/compile-errors");
        var errors = Fixture<PineErrorsResult>("pine/errors-present");

        Assert.True(compile.Success);
        Assert.True(compile.HasErrors);
        var error = Assert.Single(compile.Errors);
        Assert.Equal(3, error.Line);
        Assert.Equal(6, error.Column);
        Assert.Equal(8, error.Severity);
        Assert.Equal(PineMarkerSeverity.Error, error.Level);
        Assert.Contains("undefined_variable", error.Message);
        Assert.Equal(1, errors.ErrorCount);
        Assert.Equal(error.Message, errors.Errors[0].Message);
    }

    [Fact]
    public void Pine_marker_level_tolerates_unknown_severities()
    {
        var marker = JsonSerializer.Deserialize("""{"success":true,"has_errors":true,"error_count":1,"errors":[{"line":1,"column":1,"message":"x","severity":3}]}""",
            TvJsonContext.Default.PineErrorsResult)!;

        Assert.Equal(PineMarkerSeverity.Unknown, marker.Errors[0].Level);
        Assert.Equal(3, marker.Errors[0].Severity);
    }

    [Fact]
    public void Pine_analyze_maps_severities()
    {
        var clean = Fixture<PineAnalyzeResult>("pine/analyze");
        var issues = Fixture<PineAnalyzeResult>("pine/analyze-issues");

        Assert.Equal(0, clean.IssueCount);
        Assert.NotNull(clean.Note);
        Assert.Equal(3, issues.IssueCount);
        Assert.Equal(
            [PineDiagnosticSeverity.Error, PineDiagnosticSeverity.Warning, PineDiagnosticSeverity.Error],
            issues.Diagnostics.Select(d => d.Severity));
        Assert.Equal(4, issues.Diagnostics[0].Line);
    }

    [Fact]
    public void Pine_diagnostic_severity_tolerates_new_values()
    {
        var result = JsonSerializer.Deserialize("""{"success":true,"issue_count":1,"diagnostics":[{"line":1,"column":1,"message":"m","severity":"hint"}]}""",
            TvJsonContext.Default.PineAnalyzeResult)!;

        Assert.Equal(PineDiagnosticSeverity.Unknown, result.Diagnostics[0].Severity);
    }

    [Fact]
    public void Pine_check_distinguishes_compiled_from_failed()
    {
        var ok = Fixture<PineCheckResult>("pine/check");
        var failed = Fixture<PineCheckResult>("pine/check-errors");

        Assert.True(ok.Success);
        Assert.True(ok.Compiled);
        Assert.Null(ok.Errors);
        Assert.True(failed.Success);
        Assert.False(failed.Compiled);
        var error = Assert.Single(failed.Errors!);
        Assert.Equal((3, 6, 3, 23), (error.Line, error.Column, error.EndLine, error.EndColumn));
    }

    [Fact]
    public void Pine_new_open_list_console_map()
    {
        var created = Fixture<PineNewResult>("pine/new-strategy");
        var open = Fixture<PineOpenResult>("pine/open");
        var list = Fixture<PineListResult>("pine/list");
        var console = Fixture<PineConsoleResult>("pine/console");
        var raw = Fixture<PineRawCompileResult>("pine/raw-compile");

        Assert.Equal("strategy", created.Template);
        Assert.True(open.Opened);
        Assert.True(open.Lines > 0);
        Assert.Equal(list.Count, list.Scripts.Count);
        Assert.All(list.Scripts, s => Assert.NotNull(s.ModifiedAt));
        Assert.Equal("3.0", list.Scripts[0].Version);
        Assert.Equal(console.EntryCount, console.Entries.Count);
        Assert.All(console.Entries, e => Assert.Equal(PineConsoleEntryType.Info, e.Type));
        Assert.Equal("dom_fallback", raw.Source);
    }

    [Fact]
    public void Pine_compile_study_added_shape_from_the_contract_parses()
    {
        var compile = JsonSerializer.Deserialize(
            """{"success":true,"button_clicked":"Add to chart","has_errors":false,"errors":[],"study_added":true}""",
            TvJsonContext.Default.PineCompileResult)!;
        var save = JsonSerializer.Deserialize("""{"success":true,"action":"Ctrl+S_dispatched"}""", TvJsonContext.Default.PineSaveResult)!;

        Assert.True(compile.StudyAdded);
        Assert.Equal("Ctrl+S_dispatched", save.Action);
        Assert.Empty(Fixtures.FindUnmappedFields(compile));
    }

    // --- Indicators ---------------------------------------------------------------------------------------

    [Fact]
    public void Indicator_add_variants_map()
    {
        var add = Fixture<IndicatorAddResult>("indicators/add");
        var unknown = Fixture<IndicatorAddResult>("indicators/add-unknown");
        var withInputs = Fixture<IndicatorAddResult>("indicators/add-with-inputs");

        Assert.Equal("Volume", add.Indicator);
        Assert.False(string.IsNullOrEmpty(add.EntityId));
        Assert.Equal(1, add.NewStudyCount);
        Assert.Null(add.Inputs);
        Assert.False(unknown.Success);
        Assert.Null(unknown.EntityId);
        Assert.Equal(7, withInputs.Inputs!.Applied!["length"].GetInt32());
        Assert.Equal(["no_such_input"], withInputs.Inputs.UnknownInputs);
    }

    [Fact]
    public void Indicator_get_set_toggle_remove_map()
    {
        var info = Fixture<IndicatorInfoResult>("indicators/get");
        var set = Fixture<IndicatorSetInputsResult>("indicators/set");
        var hidden = Fixture<IndicatorVisibilityResult>("indicators/toggle-hidden");
        var visible = Fixture<IndicatorVisibilityResult>("indicators/toggle-visible");
        var removed = Fixture<IndicatorRemoveResult>("indicators/remove");

        Assert.Contains(info.Inputs!, i => i.Id == "source" && i.Value.GetString() == "close");
        Assert.Equal(21, set.UpdatedInputs["length"].GetInt32());
        Assert.False(hidden.Visible);
        Assert.True(visible.Visible);
        Assert.Equal("remove", removed.Action);
        Assert.False(string.IsNullOrEmpty(removed.EntityId));
    }

    // --- Replay -------------------------------------------------------------------------------------------

    [Fact]
    public void Replay_start_and_step_expose_timestamps()
    {
        var start = Fixture<ReplayStartResult>("replay/start");
        var step = Fixture<ReplayStepResult>("replay/step");

        Assert.True(start.ReplayStarted);
        Assert.Equal("(first available)", start.Date);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1633899300), start.CurrentDate);
        Assert.True(step.CurrentDate > start.CurrentDate);
    }

    [Fact]
    public void Replay_status_stopped_and_started()
    {
        var stopped = Fixture<ReplayStatusResult>("replay/status-stopped");
        var started = Fixture<ReplayStatusResult>("replay/status-started");

        Assert.True(stopped.IsReplayAvailable);
        Assert.False(stopped.IsReplayStarted);
        Assert.Null(stopped.CurrentDate);
        Assert.Equal("ActiveChart", stopped.ReplayMode);
        Assert.True(started.IsReplayStarted);
        Assert.NotNull(started.CurrentDate);
        Assert.Equal(10000, started.AutoplayDelay);
        Assert.Null(started.Position);
        Assert.Null(started.RealizedPnl);
    }

    [Fact]
    public void Replay_trade_autoplay_and_stop_map()
    {
        var buy = Fixture<ReplayTradeResult>("replay/trade-buy");
        var on = Fixture<ReplayAutoplayResult>("replay/autoplay-on");
        var off = Fixture<ReplayAutoplayResult>("replay/autoplay-off");
        var stop = Fixture<ReplayStopResult>("replay/stop");
        var again = Fixture<ReplayStopResult>("replay/stop-again");

        Assert.Equal("buy", buy.Action);
        Assert.True(on.AutoplayActive);
        Assert.Equal(1000, on.DelayMs);
        Assert.False(off.AutoplayActive);
        Assert.True(stop.WasRunning);
        Assert.False(again.WasRunning);
    }

    [Fact]
    public void Replay_position_values_parse_when_reported()
    {
        var trade = JsonSerializer.Deserialize("""{"success":true,"action":"sell","position":-1,"realized_pnl":12.5}""", TvJsonContext.Default.ReplayTradeResult)!;

        Assert.Equal(-1, trade.Position);
        Assert.Equal(12.5, trade.RealizedPnl);
    }

    // --- Chart mutation results ---------------------------------------------------------------------------

    [Fact]
    public void Chart_mutation_fixtures_map()
    {
        var symbol = Fixture<SetSymbolResult>("chart/set-symbol");
        var timeframe = Fixture<SetTimeframeResult>("chart/set-timeframe");
        var type = Fixture<ChartTypeResult>("chart/set-type");
        var range = Fixture<SetVisibleRangeResult>("chart/set-range");
        var scroll = Fixture<ScrollResult>("chart/scroll");

        Assert.Equal("NASDAQ:AAPL", symbol.Symbol);
        Assert.Equal("60", timeframe.Timeframe);
        Assert.True(timeframe.ChartReady);
        Assert.Equal(ChartType.Candles, type.Type);
        Assert.Equal("Candles", type.ChartTypeName);
        Assert.True(range.Actual.To > range.Actual.From);
        Assert.Equal("2026-10-01", scroll.Date);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), scroll.CenteredOn);
    }
}
