using System.Text.Json;
using System.Text.Json.Nodes;
using TradingView.Client.Tests.Support;

namespace TradingView.Client.Tests;

public partial class Stage4ApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static FakeTransport Respond(string fixture) => new(_ => new TvRawResult(0, Fixtures.Read(fixture + ".json"), string.Empty));

    private static readonly DateTimeOffset T0 = new(2025, 1, 15, 12, 0, 0, TimeSpan.Zero); // 1736942400

    // --- Drawings -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Draw_builds_equals_form_arguments_including_negative_prices()
    {
        var transport = Respond("drawings/shape-trend");
        using var client = transport.CreateClient();

        await client.Drawings.DrawAsync(
            new ShapeRequest(
                ShapeType.TrendLine,
                new ShapePoint(T0, -12.5),
                new ShapePoint(T0.AddHours(1), 7900.25),
                text: "-note",
                overrides: new JsonObject { ["linecolor"] = "#ff0000" }),
            Ct);

        Assert.Equal(
            [
                "draw", "shape", "--type=trend_line", "--price=-12.5", "--time=1736942400",
                "--price2=7900.25", "--time2=1736946000", "--text=-note", "--overrides={\"linecolor\":\"#ff0000\"}",
            ],
            transport.LastArgs);
    }

    [Fact]
    public async Task Draw_with_a_raw_shape_name_and_single_point()
    {
        var transport = Respond("drawings/shape-vertical");
        using var client = transport.CreateClient();

        await client.Drawings.DrawAsync(new ShapeRequest("some_future_shape", new ShapePoint(T0, 1)), Ct);

        Assert.Equal(["draw", "shape", "--type=some_future_shape", "--price=1", "--time=1736942400"], transport.LastArgs);
    }

    [Theory]
    [InlineData(ShapeType.HorizontalLine, "horizontal_line")]
    [InlineData(ShapeType.ExtendedLine, "extended")]
    [InlineData(ShapeType.PriceLabel, "price_label")]
    [InlineData(ShapeType.FibRetracement, "fib_retracement")]
    [InlineData(ShapeType.LongPosition, "long_position")]
    public void Shape_types_map_to_tradingview_names(ShapeType type, string wire)
        => Assert.Equal(wire, new ShapeRequest(type, new ShapePoint(T0, 1)).Shape);

    [Fact]
    public void Every_shape_type_has_a_unique_snake_case_name()
    {
        var names = Enum.GetValues<ShapeType>().Select(t => new ShapeRequest(t, new ShapePoint(T0, 1)).Shape).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, n => Assert.Matches("^[a-z_]+$", n));
    }

    [Fact]
    public async Task Draw_helpers_pick_the_right_shape_and_default_the_time()
    {
        var transport = Respond("drawings/shape-horizontal");
        using var client = transport.CreateClient();
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await client.Drawings.DrawHorizontalLineAsync(7866, cancellationToken: Ct);
        await client.Drawings.DrawRectangleAsync(new ShapePoint(T0, 1), new ShapePoint(T0.AddMinutes(5), 2), cancellationToken: Ct);
        await client.Drawings.DrawTextAsync("hello", new ShapePoint(T0, 3), cancellationToken: Ct);

        var h = transport.Commands[0].Args;
        Assert.Equal("--type=horizontal_line", h[2]);
        Assert.InRange(long.Parse(h[4]["--time=".Length..]), before, before + 60);
        Assert.Equal("--type=rectangle", transport.Commands[1].Args[2]);
        Assert.Contains("--text=hello", transport.Commands[2].Args);
    }

    [Fact]
    public async Task Draw_rejects_non_finite_prices_and_blank_shapes()
    {
        var transport = Respond("drawings/shape-horizontal");
        using var client = transport.CreateClient();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Drawings.DrawHorizontalLineAsync(double.NaN, cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Drawings.DrawHorizontalLineAsync(double.PositiveInfinity, cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Drawings.DrawAsync(new ShapeRequest(" ", new ShapePoint(T0, 1)), Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.Drawings.DrawAsync(null!, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Drawings.DrawTextAsync("", new ShapePoint(T0, 1), cancellationToken: Ct));
        Assert.Empty(transport.Commands);
    }

    [Fact]
    public async Task Drawing_management_commands()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, Fixtures.Read(cmd.Args[1] switch
        {
            "list" => "drawings/list.json",
            "get" => "drawings/get.json",
            "remove" => "drawings/remove.json",
            _ => "drawings/clear.json",
        }), string.Empty));
        using var client = transport.CreateClient();

        await client.Drawings.ListAsync(Ct);
        await client.Drawings.GetAsync("abc", Ct);
        await client.Drawings.RemoveAsync("abc", Ct);
        await client.Drawings.ClearAsync(Ct);

        Assert.Equal([["draw", "list"], ["draw", "get", "abc"], ["draw", "remove", "abc"], ["draw", "clear"]], transport.Commands.Select(c => c.Args.ToArray()));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Drawings.GetAsync(" ", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Drawings.RemoveAsync("", Ct));
    }

    // --- Alerts -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(AlertCondition.Crossing, "crossing")]
    [InlineData(AlertCondition.GreaterThan, "greater_than")]
    [InlineData(AlertCondition.LessThan, "less_than")]
    public async Task Alert_create_sends_condition_and_message(AlertCondition condition, string wire)
    {
        var transport = Respond("alerts/create");
        using var client = transport.CreateClient();

        await client.Alerts.CreateAsync(7900.5, condition, "-- hit it", Ct);

        Assert.Equal(["alert", "create", "--price=7900.5", $"--condition={wire}", "--message=-- hit it"], transport.LastArgs);
    }

    [Fact]
    public async Task Alert_create_without_message_and_with_failure()
    {
        var ok = Respond("alerts/create");
        using var client = ok.CreateClient();
        await client.Alerts.CreateAsync(100, cancellationToken: Ct);
        Assert.Equal(["alert", "create", "--price=100", "--condition=crossing"], ok.LastArgs);

        var failing = new FakeTransport("""{"success":false,"error":"Alert service unavailable"}""");
        using var failingClient = failing.CreateClient();
        var ex = await Assert.ThrowsAsync<TvOperationFailedException>(() => failingClient.Alerts.CreateAsync(1, cancellationToken: Ct));
        Assert.Equal("Alert service unavailable", ex.Message);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => failingClient.Alerts.CreateAsync(double.NaN, cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => failingClient.Alerts.CreateAsync(1, (AlertCondition)9, cancellationToken: Ct));
    }

    [Fact]
    public async Task Alert_list_and_delete_commands()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, Fixtures.Read(cmd.Args[1] == "list" ? "alerts/list.json" : "alerts/delete.json"), string.Empty));
        using var client = transport.CreateClient();

        await client.Alerts.ListAsync(Ct);
        await client.Alerts.DeleteAsync(5817021868, Ct);
        await client.Alerts.DeleteAllAsync(Ct);

        Assert.Equal([["alert", "list"], ["alert", "delete", "--id=5817021868"], ["alert", "delete", "--all"]], transport.Commands.Select(c => c.Args.ToArray()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Alerts.DeleteAsync(0, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Alerts.DeleteAsync(-3, Ct));
    }

    // --- Watchlist ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Watchlist_commands_send_symbols_as_separate_arguments()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, Fixtures.Read(cmd.Args[1] switch
        {
            "get" => "watchlist/get.json",
            "add" => "watchlist/add.json",
            "add-bulk" => "watchlist/add-bulk.json",
            _ => "watchlist/remove.json",
        }), string.Empty));
        using var client = transport.CreateClient();

        await client.Watchlist.GetAsync(Ct);
        await client.Watchlist.AddAsync("NASDAQ:AAPL", Ct);
        await client.Watchlist.AddRangeAsync(["NASDAQ:NVDA", "CME_MINI:ES1!"], Ct);
        await client.Watchlist.RemoveAsync("NASDAQ:AAPL", "NASDAQ:NVDA");
        await client.Watchlist.RemoveAsync(new List<string> { "A", "B" }, Ct);

        Assert.Equal(
            [
                ["watchlist", "get"], ["watchlist", "add", "NASDAQ:AAPL"], ["watchlist", "add-bulk", "NASDAQ:NVDA", "CME_MINI:ES1!"],
                ["watchlist", "remove", "NASDAQ:AAPL", "NASDAQ:NVDA"], ["watchlist", "remove", "A", "B"],
            ],
            transport.Commands.Select(c => c.Args.ToArray()));
    }

    [Fact]
    public async Task Watchlist_input_is_validated()
    {
        var transport = new FakeTransport("{}");
        using var client = transport.CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() => client.Watchlist.AddAsync(" ", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Watchlist.AddRangeAsync([], Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Watchlist.AddRangeAsync(["A", ""], Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Watchlist.RemoveAsync());
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.Watchlist.AddRangeAsync(null!, Ct));
        Assert.Empty(transport.Commands);
    }

    [Fact]
    public async Task Watchlist_soft_failures_throw_with_typed_result()
    {
        var transport = new FakeTransport("""{"success":false,"removed":[],"skipped":["X"],"error":"nothing removed"}""");
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvOperationFailedException>(() => client.Watchlist.RemoveAsync("X"));

        Assert.Equal(["X"], Assert.IsType<WatchlistRemoveResult>(ex.Result).Skipped);
    }

    // --- Layouts / panes / tabs ---------------------------------------------------------------------------

    [Fact]
    public async Task Layout_commands_keep_names_with_spaces_as_one_argument()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, Fixtures.Read(cmd.Args[1] == "list" ? "layouts/list.json" : "layouts/switch.json"), string.Empty));
        using var client = transport.CreateClient();

        await client.Layouts.ListAsync(Ct);
        await client.Layouts.SwitchAsync("My Layout (2)", Ct);

        Assert.Equal([["layout", "list"], ["layout", "switch", "My Layout (2)"]], transport.Commands.Select(c => c.Args.ToArray()));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Layouts.SwitchAsync("", Ct));
    }

    [Theory]
    [InlineData(PaneLayout.Single, "s")]
    [InlineData(PaneLayout.TwoHorizontal, "2h")]
    [InlineData(PaneLayout.TwoVertical, "2v")]
    [InlineData(PaneLayout.TwoOne, "2-1")]
    [InlineData(PaneLayout.OneTwo, "1-2")]
    [InlineData(PaneLayout.ThreeHorizontal, "3h")]
    [InlineData(PaneLayout.ThreeVertical, "3v")]
    [InlineData(PaneLayout.ThreeSplit, "3s")]
    [InlineData(PaneLayout.Grid2x2, "4")]
    [InlineData(PaneLayout.FourHorizontal, "4h")]
    [InlineData(PaneLayout.FourVertical, "4v")]
    [InlineData(PaneLayout.FourSplit, "4s")]
    [InlineData(PaneLayout.Six, "6")]
    [InlineData(PaneLayout.Eight, "8")]
    [InlineData(PaneLayout.Ten, "10")]
    [InlineData(PaneLayout.Twelve, "12")]
    [InlineData(PaneLayout.Fourteen, "14")]
    [InlineData(PaneLayout.Sixteen, "16")]
    public async Task Pane_layouts_map_to_cli_codes(PaneLayout layout, string code)
    {
        var transport = Respond("panes/set-layout");
        using var client = transport.CreateClient();

        await client.Panes.SetLayoutAsync(layout, Ct);

        Assert.Equal(["pane", "layout", code], transport.LastArgs);
    }

    [Fact]
    public void Every_pane_layout_is_covered_by_a_theory_case()
        => Assert.Equal(18, Enum.GetValues<PaneLayout>().Length);

    [Fact]
    public async Task Pane_list_focus_and_symbol_commands()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, Fixtures.Read(cmd.Args[1] switch
        {
            "list" => "panes/list.json",
            "focus" => "panes/focus.json",
            _ => "panes/set-symbol.json",
        }), string.Empty));
        using var client = transport.CreateClient();

        await client.Panes.ListAsync(Ct);
        await client.Panes.FocusAsync(1, Ct);
        await client.Panes.SetSymbolAsync(1, "NASDAQ:AAPL", Ct);

        Assert.Equal([["pane", "list"], ["pane", "focus", "1"], ["pane", "symbol", "1", "NASDAQ:AAPL"]], transport.Commands.Select(c => c.Args.ToArray()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Panes.FocusAsync(-1, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Panes.SetSymbolAsync(-1, "X", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Panes.SetSymbolAsync(0, " ", Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Panes.SetLayoutAsync((PaneLayout)99, Ct));
    }

    [Fact]
    public async Task Pane_unknown_layout_error_is_a_command_exception()
    {
        var transport = new FakeTransport("", 1, """{"success":false,"error":"Unknown layout \"16\". Available layouts:\n  s ..."}""");
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvCommandException>(() => client.Panes.SetLayoutAsync(PaneLayout.Sixteen, Ct));

        Assert.StartsWith("Unknown layout", ex.Message);
    }

    [Fact]
    public async Task Tab_commands()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, Fixtures.Read(cmd.Args[1] switch
        {
            "list" => "tabs/list.json",
            "new" => "tabs/new.json",
            "close" => "tabs/close.json",
            _ => "tabs/switch.json",
        }), string.Empty));
        using var client = transport.CreateClient();

        await client.Tabs.ListAsync(Ct);
        await client.Tabs.NewAsync(Ct);
        await client.Tabs.CloseAsync(Ct);
        await client.Tabs.SwitchAsync(0, Ct);

        Assert.Equal([["tab", "list"], ["tab", "new"], ["tab", "close"], ["tab", "switch", "0"]], transport.Commands.Select(c => c.Args.ToArray()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Tabs.SwitchAsync(-1, Ct));
    }

    [Fact]
    public async Task Closing_the_last_tab_is_a_command_exception()
    {
        var transport = new FakeTransport("", 1, """{"success":false,"error":"Cannot close the last tab. Use `tv launch` to restart TradingView instead."}""");
        using var client = transport.CreateClient();

        await Assert.ThrowsAsync<TvCommandException>(() => client.Tabs.CloseAsync(Ct));
    }

    // --- Screenshot ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(ScreenshotRegion.Full, "full")]
    [InlineData(ScreenshotRegion.Chart, "chart")]
    [InlineData(ScreenshotRegion.StrategyTester, "strategy_tester")]
    public async Task Screenshot_region_and_file_name(ScreenshotRegion region, string wire)
    {
        var transport = Respond("screenshot/chart");
        using var client = transport.CreateClient();

        await client.Screenshots.CaptureAsync(region, cancellationToken: Ct);
        await client.Screenshots.CaptureAsync(region, "my shot", Ct);

        Assert.Equal(["screenshot", "--region", wire], transport.Commands[0].Args);
        Assert.Equal(["screenshot", "--region", wire, "--output=my shot"], transport.Commands[1].Args);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Screenshots.CaptureAsync((ScreenshotRegion)7, cancellationToken: Ct));
    }

    // --- Ui -----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(UiSelector.Text, "text")]
    [InlineData(UiSelector.AriaLabel, "aria-label")]
    [InlineData(UiSelector.DataName, "data-name")]
    [InlineData(UiSelector.ClassContains, "class-contains")]
    public async Task Ui_click_and_hover_use_the_selector_and_equals_form_value(UiSelector by, string wire)
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, Fixtures.Read(cmd.Args[1] == "click" ? "ui/click.json" : "ui/hover.json"), string.Empty));
        using var client = transport.CreateClient();

        await client.Ui.ClickAsync(by, "-weird value", Ct);
        await client.Ui.HoverAsync(by, "Indicators, metrics", Ct);

        Assert.Equal(["ui", "click", "--by", wire, "--value=-weird value"], transport.Commands[0].Args);
        Assert.Equal(["ui", "hover", "--by", wire, "--value=Indicators, metrics"], transport.Commands[1].Args);
    }

    [Theory]
    [InlineData(KeyModifiers.None, new string[0])]
    [InlineData(KeyModifiers.Ctrl, new[] { "--ctrl" })]
    [InlineData(KeyModifiers.Ctrl | KeyModifiers.Shift, new[] { "--ctrl", "--shift" })]
    [InlineData(KeyModifiers.Alt | KeyModifiers.Meta, new[] { "--alt", "--meta" })]
    [InlineData(KeyModifiers.Ctrl | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Meta, new[] { "--ctrl", "--shift", "--alt", "--meta" })]
    public async Task Ui_keyboard_sends_modifier_flags(KeyModifiers modifiers, string[] flags)
    {
        var transport = Respond("ui/keyboard");
        using var client = transport.CreateClient();

        await client.Ui.PressKeyAsync("k", modifiers, Ct);

        Assert.Equal(["ui", "keyboard", "k", .. flags], transport.LastArgs);
    }

    [Fact]
    public async Task Ui_scroll_find_type_panel_fullscreen_mouse()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, Fixtures.Read(cmd.Args[1] switch
        {
            "scroll" => "ui/scroll.json",
            "find" => "ui/find.json",
            "type" => "ui/type.json",
            "panel" => "ui/panel.json",
            "fullscreen" => "ui/fullscreen.json",
            _ => "ui/mouse.json",
        }), string.Empty));
        using var client = transport.CreateClient();

        await client.Ui.ScrollAsync(cancellationToken: Ct);
        await client.Ui.ScrollAsync(ScrollDirection.Left, 120, Ct);
        await client.Ui.FindAsync("Indicators", FindStrategy.AriaLabel, Ct);
        await client.Ui.TypeTextAsync("-rsi value", Ct);
        await client.Ui.PanelAsync(PanelName.PineEditor, PanelAction.Open, Ct);
        await client.Ui.PanelAsync(PanelName.StrategyTester, cancellationToken: Ct);
        await client.Ui.ToggleFullscreenAsync(Ct);
        await client.Ui.MouseClickAsync(100.5, 200, rightButton: true, doubleClick: true, cancellationToken: Ct);
        await client.Ui.MouseClickAsync(-5, 0, cancellationToken: Ct);

        Assert.Equal(
            [
                ["ui", "scroll", "down"], ["ui", "scroll", "left", "--amount=120"], ["ui", "find", "Indicators", "--strategy", "aria-label"],
                ["ui", "type", "-rsi value"], ["ui", "panel", "pine-editor", "open"], ["ui", "panel", "strategy-tester", "toggle"],
                ["ui", "fullscreen"], ["ui", "mouse", "100.5", "200", "--right", "--double"], ["ui", "mouse", "-5", "0"],
            ],
            transport.Commands.Select(c => c.Args.ToArray()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Ui.ScrollAsync(ScrollDirection.Up, 0, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Ui.MouseClickAsync(double.NaN, 1, cancellationToken: Ct));
    }

    [Theory]
    [InlineData(PanelName.PineEditor, "pine-editor")]
    [InlineData(PanelName.StrategyTester, "strategy-tester")]
    [InlineData(PanelName.Watchlist, "watchlist")]
    [InlineData(PanelName.Alerts, "alerts")]
    [InlineData(PanelName.Trading, "trading")]
    public async Task Ui_panel_names(PanelName panel, string wire)
    {
        var transport = Respond("ui/panel");
        using var client = transport.CreateClient();

        await client.Ui.PanelAsync(panel, PanelAction.Close, Ct);

        Assert.Equal(["ui", "panel", wire, "close"], transport.LastArgs);
    }

    [Fact]
    public async Task Ui_input_is_validated()
    {
        var transport = new FakeTransport("{}");
        using var client = transport.CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() => client.Ui.ClickAsync(UiSelector.Text, "", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Ui.PressKeyAsync("", cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Ui.FindAsync(" ", cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Ui.TypeTextAsync("", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Ui.EvaluateAsync(" ", Ct));
        Assert.Empty(transport.Commands);
    }

    [Fact]
    public async Task Ui_click_without_a_match_is_a_command_exception()
    {
        var transport = new FakeTransport("", 1, """{"success":false,"error":"No matching element found for text=\"Nope\""}""");
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvCommandException>(() => client.Ui.ClickAsync(UiSelector.Text, "Nope", Ct));

        Assert.Contains("No matching element", ex.Message);
    }

    // --- Ui.Evaluate --------------------------------------------------------------------------------------

    [Fact]
    public async Task Evaluate_returns_the_raw_json_value()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, cmd.Args[2] switch
        {
            "1+1" => Fixtures.Read("ui/eval-number.json"),
            "obj" => Fixtures.Read("ui/eval-object.json"),
            _ => Fixtures.Read("ui/eval-undefined.json"),
        }, string.Empty));
        using var client = transport.CreateClient();

        var number = await client.Ui.EvaluateAsync("1+1", Ct);
        var obj = await client.Ui.EvaluateAsync("obj", Ct);
        var undefined = await client.Ui.EvaluateAsync("undefined", Ct);

        Assert.Equal(2, number.GetInt32());
        Assert.Equal("x", obj.GetProperty("d").GetString());
        Assert.Equal(JsonValueKind.Null, undefined.ValueKind);
        Assert.Equal(["ui", "eval", "1+1"], transport.Commands[0].Args);
    }

    public sealed record Sample(int A, int[] B, string D);

    [System.Text.Json.Serialization.JsonSerializable(typeof(Sample))]
    [System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
    public sealed partial class SampleContext : System.Text.Json.Serialization.JsonSerializerContext;

    [Fact]
    public async Task Evaluate_generic_deserializes_with_type_info_and_maps_null_to_default()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, Fixtures.Read(cmd.Args[2] == "obj" ? "ui/eval-object.json" : "ui/eval-undefined.json"), string.Empty));
        using var client = transport.CreateClient();

        var sample = await client.Ui.EvaluateAsync("obj", SampleContext.Default.Sample, Ct);
        var nothing = await client.Ui.EvaluateAsync("undefined", SampleContext.Default.Sample, Ct);

        Assert.Equal(new[] { 1, 2 }, sample!.B);
        Assert.Equal(1, sample.A);
        Assert.Null(nothing);
    }

    [Fact]
    public async Task Evaluate_expression_with_spaces_stays_one_argument()
    {
        var transport = Respond("ui/eval-number");
        using var client = transport.CreateClient();

        await client.Ui.EvaluateAsync("(function() { return 'a b' })()", Ct);

        Assert.Equal(["ui", "eval", "(function() { return 'a b' })()"], transport.LastArgs);
    }
}
