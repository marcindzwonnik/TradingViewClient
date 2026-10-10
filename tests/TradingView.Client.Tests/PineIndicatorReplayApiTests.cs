using System.Text.Json.Nodes;
using TradingView.Client.Tests.Support;

namespace TradingView.Client.Tests;

public class PineIndicatorReplayApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static FakeTransport Respond(string fixture)
        => new(_ => new TvRawResult(0, Fixtures.Read(fixture + ".json"), string.Empty));

    // --- Pine ---------------------------------------------------------------------------------------------

    private const string TrickySource = "//@version=6\nindicator(\"A & B ^ \\\"q\\\" %PATH% !\")\nplot(close) // zażółć ✓\n";

    [Fact]
    public async Task Pine_source_commands_send_text_over_stdin_untouched()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, cmd.Args[1] switch
        {
            "set" => Fixtures.Read("pine/set.json"),
            "analyze" => Fixtures.Read("pine/analyze.json"),
            _ => Fixtures.Read("pine/check.json"),
        }, string.Empty));
        using var client = transport.CreateClient();

        await client.Pine.SetSourceAsync(TrickySource, Ct);
        await client.Pine.AnalyzeAsync(TrickySource, Ct);
        await client.Pine.CheckAsync(TrickySource, Ct);

        Assert.Equal([["pine", "set"], ["pine", "analyze"], ["pine", "check"]], transport.Commands.Select(c => c.Args.ToArray()));
        Assert.All(transport.Commands, c => Assert.Equal(TrickySource, c.StdIn));
    }

    [Fact]
    public async Task Pine_source_must_not_be_blank()
    {
        var transport = new FakeTransport("{}");
        using var client = transport.CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() => client.Pine.SetSourceAsync("  \n", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Pine.AnalyzeAsync("", Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.Pine.CheckAsync(null!, Ct));
        Assert.Empty(transport.Commands);
    }

    [Theory]
    [InlineData(PineScriptType.Indicator, "indicator")]
    [InlineData(PineScriptType.Strategy, "strategy")]
    [InlineData(PineScriptType.Library, "library")]
    public async Task Pine_new_sends_lowercase_type(PineScriptType type, string wire)
    {
        var transport = Respond("pine/new");
        using var client = transport.CreateClient();

        await client.Pine.NewAsync(type, Ct);

        Assert.Equal(["pine", "new", wire], transport.LastArgs);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Pine.NewAsync((PineScriptType)9, Ct));
    }

    [Fact]
    public async Task Pine_simple_commands_use_their_subcommands()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, cmd.Args[1] switch
        {
            "get" => Fixtures.Read("pine/get.json"),
            "compile" => Fixtures.Read("pine/compile.json"),
            "raw-compile" => Fixtures.Read("pine/raw-compile.json"),
            "save" => """{"success":true,"action":"saved_with_dialog"}""",
            "open" => Fixtures.Read("pine/open.json"),
            "list" => Fixtures.Read("pine/list.json"),
            "errors" => Fixtures.Read("pine/errors.json"),
            _ => Fixtures.Read("pine/console.json"),
        }, string.Empty));
        using var client = transport.CreateClient();

        await client.Pine.GetSourceAsync(Ct);
        await client.Pine.CompileAsync(Ct);
        await client.Pine.RawCompileAsync(Ct);
        var save = await client.Pine.SaveAsync(Ct);
        await client.Pine.OpenAsync("My Script: v2 (final)", Ct);
        await client.Pine.ListAsync(Ct);
        await client.Pine.GetErrorsAsync(Ct);
        await client.Pine.GetConsoleAsync(Ct);

        Assert.Equal(
            [["pine", "get"], ["pine", "compile"], ["pine", "raw-compile"], ["pine", "save"], ["pine", "open", "My Script: v2 (final)"], ["pine", "list"], ["pine", "errors"], ["pine", "console"]],
            transport.Commands.Select(c => c.Args.ToArray()));
        Assert.Equal("saved_with_dialog", save.Action);
        Assert.All(transport.Commands.Where(c => c.Args[1] is "compile" or "raw-compile"), c => Assert.True(c.Timeout >= TimeSpan.FromMinutes(1)));
        Assert.All(transport.Commands, c => Assert.Null(c.StdIn));
    }

    [Fact]
    public async Task Pine_new_failure_with_closed_editor_is_a_command_exception_when_auto_open_is_off()
    {
        var transport = new FakeTransport("", exitCode: 1, stdErr: """{"success":false,"error":"Could not open Pine Editor."}""");
        using var client = transport.CreateClient(new TvClientOptions { AutoOpenPineEditor = false });

        var ex = await Assert.ThrowsAsync<TvCommandException>(() => client.Pine.NewAsync(cancellationToken: Ct));

        Assert.Equal("Could not open Pine Editor.", ex.Message);
        Assert.Single(transport.Commands);
    }

    [Fact]
    public async Task Pine_editor_commands_open_the_panel_and_retry_once()
    {
        var calls = 0;
        var transport = new FakeTransport(cmd =>
        {
            if (cmd.Args[0] == "ui")
            {
                return new TvRawResult(0, """{"success":true,"panel":"pine-editor","action":"open","was_open":false,"performed":"opened"}""", "");
            }

            return ++calls == 1
                ? new TvRawResult(1, "", """{"success":false,"error":"Could not open Pine Editor."}""")
                : new TvRawResult(0, Fixtures.Read("pine/set.json"), "");
        });
        using var client = transport.CreateClient();

        var result = await client.Pine.SetSourceAsync("//@version=6\nindicator(\"x\")\n", Ct);

        Assert.Equal(5, result.LinesSet);
        Assert.Equal(
            [["pine", "set"], ["ui", "panel", "pine-editor", "open"], ["pine", "set"]],
            transport.Commands.Select(c => c.Args.ToArray()));
        Assert.Equal(transport.Commands[0].StdIn, transport.Commands[2].StdIn); // the source is resent
    }

    [Fact]
    public async Task Pine_editor_retry_does_not_swallow_other_command_errors()
    {
        var transport = new FakeTransport("", exitCode: 1, stdErr: """{"success":false,"error":"Something unrelated"}""");
        using var client = transport.CreateClient();

        await Assert.ThrowsAsync<TvCommandException>(() => client.Pine.GetSourceAsync(Ct));

        Assert.Single(transport.Commands);
    }

    [Fact]
    public async Task Pine_open_rejects_blank_names()
    {
        var transport = new FakeTransport("{}");
        using var client = transport.CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() => client.Pine.OpenAsync(" ", Ct));
        Assert.Empty(transport.Commands);
    }

    // --- Indicators ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Indicator_add_without_inputs_sends_only_the_name()
    {
        var transport = Respond("indicators/add");
        using var client = transport.CreateClient();

        await client.Indicators.AddAsync("Volume", cancellationToken: Ct);
        await client.Indicators.AddAsync("Volume", new Dictionary<string, object?>(), Ct);
        await client.Indicators.AddAsync("Volume", new JsonObject(), Ct);

        Assert.All(transport.Commands, c => Assert.Equal(["indicator", "add", "Volume"], c.Args));
    }

    [Fact]
    public async Task Indicator_inputs_are_serialized_as_json_after_dash_i()
    {
        var transport = Respond("indicators/add-with-inputs");
        using var client = transport.CreateClient();
        var inputs = new Dictionary<string, object?>
        {
            ["length"] = 14,
            ["src"] = "close",
            ["smooth"] = true,
            ["mult"] = 2.5,
            ["big"] = 5_000_000_000L,
            ["price"] = 1.25m,
            ["nothing"] = null,
            ["quote\"key"] = "va\"lue\\",
        };

        await client.Indicators.AddAsync("Relative Strength Index", inputs, Ct);

        var args = transport.LastArgs;
        Assert.Equal(["indicator", "add", "Relative Strength Index", "-i"], args.Take(4));
        var parsed = JsonNode.Parse(args[4])!.AsObject();
        Assert.Equal(14, (int)parsed["length"]!);
        Assert.Equal("close", (string)parsed["src"]!);
        Assert.True((bool)parsed["smooth"]!);
        Assert.Equal(2.5, (double)parsed["mult"]!);
        Assert.Equal(5_000_000_000L, (long)parsed["big"]!);
        Assert.Equal(1.25m, (decimal)parsed["price"]!);
        Assert.Null(parsed["nothing"]);
        Assert.Equal("va\"lue\\", (string)parsed["quote\"key"]!);
    }

    [Fact]
    public async Task Indicator_inputs_accept_json_object_and_reject_unsupported_types()
    {
        var transport = Respond("indicators/add-with-inputs");
        using var client = transport.CreateClient();

        await client.Indicators.AddAsync("X", new JsonObject { ["a"] = 1, ["nested"] = new JsonArray(1, 2) }, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => client.Indicators.AddAsync("X", new Dictionary<string, object?> { ["bad"] = new object() }, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Indicators.AddAsync("X", new Dictionary<string, object?> { ["bad"] = double.NaN }, Ct));

        Assert.Equal("""{"a":1,"nested":[1,2]}""", transport.LastArgs[4]);
        Assert.Single(transport.Commands);
    }

    [Fact]
    public async Task Indicator_add_that_creates_nothing_throws_operation_failed()
    {
        var transport = Respond("indicators/add-unknown");
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvOperationFailedException>(() => client.Indicators.AddAsync("ZZZ", cancellationToken: Ct));

        var result = Assert.IsType<IndicatorAddResult>(ex.Result);
        Assert.Equal(0, result.NewStudyCount);
    }

    [Fact]
    public async Task Indicator_commands_build_their_arguments()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, cmd.Args[1] switch
        {
            "remove" => Fixtures.Read("indicators/remove.json"),
            "toggle" => Fixtures.Read("indicators/toggle-visible.json"),
            "set" => Fixtures.Read("indicators/set.json"),
            _ => Fixtures.Read("indicators/get.json"),
        }, string.Empty));
        using var client = transport.CreateClient();

        await client.Indicators.RemoveAsync("abc123", Ct);
        await client.Indicators.SetVisibilityAsync("abc123", true, Ct);
        await client.Indicators.SetVisibilityAsync("abc123", false, Ct);
        await client.Indicators.SetInputsAsync("abc123", new Dictionary<string, object?> { ["length"] = 21 }, Ct);
        await client.Indicators.SetInputsAsync("abc123", new JsonObject { ["length"] = 5 }, Ct);
        await client.Indicators.GetAsync("abc123", Ct);

        Assert.Equal(
            [
                ["indicator", "remove", "abc123"],
                ["indicator", "toggle", "abc123", "--visible"],
                ["indicator", "toggle", "abc123", "--hidden"],
                ["indicator", "set", "abc123", "-i", """{"length":21}"""],
                ["indicator", "set", "abc123", "-i", """{"length":5}"""],
                ["indicator", "get", "abc123"],
            ],
            transport.Commands.Select(c => c.Args.ToArray()));
    }

    [Fact]
    public async Task Indicator_arguments_are_validated()
    {
        var transport = new FakeTransport("{}");
        using var client = transport.CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() => client.Indicators.AddAsync(" ", cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Indicators.RemoveAsync("", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Indicators.SetInputsAsync("id", new Dictionary<string, object?>(), Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Indicators.SetInputsAsync("id", new JsonObject(), Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.Indicators.SetInputsAsync("id", (IReadOnlyDictionary<string, object?>)null!, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Indicators.GetAsync(" ", Ct));
        Assert.Empty(transport.Commands);
    }

    // --- Replay -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Replay_start_sends_iso_date_regardless_of_culture()
    {
        var transport = Respond("replay/start");
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
        try
        {
            using var client = transport.CreateClient();
            await client.Replay.StartAsync(cancellationToken: Ct);
            await client.Replay.StartAsync(new DateOnly(2026, 1, 5), Ct);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }

        Assert.Equal([["replay", "start"], ["replay", "start", "--date", "2026-01-05"]], transport.Commands.Select(c => c.Args.ToArray()));
    }

    [Fact]
    public async Task Replay_simple_commands()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, cmd.Args[1] switch
        {
            "step" => Fixtures.Read("replay/step.json"),
            "stop" => Fixtures.Read("replay/stop.json"),
            _ => Fixtures.Read("replay/status-started.json"),
        }, string.Empty));
        using var client = transport.CreateClient();

        await client.Replay.StepAsync(Ct);
        await client.Replay.StopAsync(Ct);
        await client.Replay.GetStatusAsync(Ct);

        Assert.Equal([["replay", "step"], ["replay", "stop"], ["replay", "status"]], transport.Commands.Select(c => c.Args.ToArray()));
    }

    [Theory]
    [InlineData(ReplaySpeed.Ms100, "100")]
    [InlineData(ReplaySpeed.Ms143, "143")]
    [InlineData(ReplaySpeed.Ms10000, "10000")]
    public async Task Replay_autoplay_sends_the_delay(ReplaySpeed speed, string ms)
    {
        var transport = Respond("replay/autoplay-on");
        using var client = transport.CreateClient();

        await client.Replay.ToggleAutoplayAsync(speed, Ct);
        await client.Replay.ToggleAutoplayAsync(cancellationToken: Ct);

        Assert.Equal(["replay", "autoplay", "--speed", ms], transport.Commands[0].Args);
        Assert.Equal(["replay", "autoplay"], transport.Commands[1].Args);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Replay.ToggleAutoplayAsync((ReplaySpeed)7, Ct));
    }

    [Fact]
    public void Replay_speeds_match_the_values_the_cli_accepts()
        => Assert.Equal([100, 143, 200, 300, 1000, 2000, 3000, 5000, 10000], Enum.GetValues<ReplaySpeed>().Select(s => (int)s));

    [Theory]
    [InlineData(ReplayTradeAction.Buy, "buy")]
    [InlineData(ReplayTradeAction.Sell, "sell")]
    [InlineData(ReplayTradeAction.Close, "close")]
    public async Task Replay_trade_sends_lowercase_action(ReplayTradeAction action, string wire)
    {
        var transport = Respond("replay/trade-buy");
        using var client = transport.CreateClient();

        await client.Replay.TradeAsync(action, Ct);

        Assert.Equal(["replay", "trade", wire], transport.LastArgs);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Replay.TradeAsync((ReplayTradeAction)5, Ct));
    }

    [Fact]
    public async Task Replay_step_without_replay_is_a_command_exception()
    {
        var transport = new FakeTransport("", exitCode: 1, stdErr: """{"success":false,"error":"Replay is not started. Use replay_start first."}""");
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvCommandException>(() => client.Replay.StepAsync(Ct));

        Assert.Contains("not started", ex.Message);
    }
}
