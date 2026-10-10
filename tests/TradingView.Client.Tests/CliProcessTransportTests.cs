using System.Diagnostics;
using System.Text.Json;

namespace TradingView.Client.Tests;

/// <summary>
/// Exercises the real process transport against a tiny fake CLI (a node script), so no TradingView is needed.
/// The fake CLI is controlled by its first argument.
/// </summary>
public sealed class CliProcessTransportTests : IDisposable
{
    private const string FakeCli = """
        const [mode, ...rest] = process.argv.slice(2);
        const readStdin = () => new Promise(r => { let d = ''; process.stdin.setEncoding('utf8'); process.stdin.on('data', c => d += c); process.stdin.on('end', () => r(d)); });
        switch (mode) {
          case 'echo': {
            const stdin = await readStdin();
            console.log(JSON.stringify({ success: true, args: rest, stdin, cwd: process.cwd(), host: process.env.TV_CDP_HOST ?? null, port: process.env.TV_CDP_PORT ?? null }, null, 2));
            break;
          }
          case 'big': {
            console.log(JSON.stringify({ success: true, blob: 'x'.repeat(Number(rest[0])) }, null, 2));
            console.error('e'.repeat(Number(rest[0])));
            break;
          }
          case 'fail': console.error(JSON.stringify({ success: false, error: rest[1] ?? 'boom' })); process.exit(Number(rest[0])); break;
          case 'hang': console.log('started'); setInterval(() => {}, 1000); break;
          case 'lines': for (let i = 0; i < Number(rest[0]); i++) console.log(JSON.stringify({ n: i })); console.log(''); break;
          case 'endless': {
            require_fs.writeFileSync(rest[0], String(process.pid));
            let i = 0; setInterval(() => console.log(JSON.stringify({ n: i++ })), 20); break;
          }
          case 'crash': console.log(rest[0] ?? '{"success":true}'); process.exit(3221226505); break;
          case 'lines-then-fail': console.log('{"n":1}'); console.error('{"success":false,"error":"CDP gone"}'); process.exit(2); break;
        }
        """;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tvclient-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _script;
    private readonly string? _node;

    public CliProcessTransportTests()
    {
        Directory.CreateDirectory(_dir);
        _script = Path.Combine(_dir, "index.mjs");
        File.WriteAllText(_script, "import require_fs from 'node:fs';\n" + FakeCli);
        _node = FindNode();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private CliProcessTransport CreateTransport(Action<TvClientOptions>? configure = null)
    {
        if (_node is null)
        {
            Assert.Skip("node was not found on PATH.");
        }

        var options = new TvClientOptions { NodePath = _node, CliScriptPath = _script };
        configure?.Invoke(options);
        return new CliProcessTransport(options);
    }

    private static string? FindNode()
    {
        var name = OperatingSystem.IsWindows() ? "node.exe" : "node";
        return (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Where(d => d.Length > 0)
            .Select(d => Path.Combine(d.Trim('"'), name))
            .FirstOrDefault(File.Exists);
    }

    [Fact]
    public async Task Arguments_with_special_characters_and_stdin_arrive_untouched()
    {
        using var transport = CreateTransport();
        string[] args = ["echo", "CME_MINI:ES1!", "a^b&c|d", "{\"length\":14,\"src\":\"close\"}", "with space", "", "100%PATH%", "quote\"inside"];
        var pine = "//@version=6\nindicator(\"x\")\nplot(close) // ünïcode ✓\n";

        var raw = await transport.ExecuteAsync(new TvCommand(args, StdIn: pine), TestContext.Current.CancellationToken);

        Assert.Equal(0, raw.ExitCode);
        using var doc = JsonDocument.Parse(raw.StdOut);
        Assert.Equal(args.Skip(1), doc.RootElement.GetProperty("args").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(pine, doc.RootElement.GetProperty("stdin").GetString());
    }

    [Fact]
    public async Task Cdp_environment_and_working_directory_are_applied()
    {
        using var transport = CreateTransport(o =>
        {
            o.CdpHost = "10.1.2.3";
            o.CdpPort = 9333;
            o.WorkingDirectory = _dir;
        });

        var raw = await transport.ExecuteAsync(TvCommand.Create("echo"), TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(raw.StdOut);
        Assert.Equal("10.1.2.3", doc.RootElement.GetProperty("host").GetString());
        Assert.Equal("9333", doc.RootElement.GetProperty("port").GetString());
        Assert.Equal(
            Path.GetFullPath(_dir).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(doc.RootElement.GetProperty("cwd").GetString()!).TrimEnd(Path.DirectorySeparatorChar),
            ignoreCase: OperatingSystem.IsWindows());
    }

    [Fact]
    public async Task Large_stdout_and_stderr_do_not_deadlock()
    {
        using var transport = CreateTransport();

        var raw = await transport.ExecuteAsync(TvCommand.Create("big", "2000000"), TestContext.Current.CancellationToken);

        Assert.Equal(0, raw.ExitCode);
        Assert.True(raw.StdOut.Length > 2_000_000);
        Assert.True(raw.StdErr.Length > 2_000_000);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Non_zero_exit_codes_are_returned_with_stderr(int exitCode)
    {
        using var transport = CreateTransport();

        var raw = await transport.ExecuteAsync(TvCommand.Create("fail", exitCode.ToString(), "bad"), TestContext.Current.CancellationToken);

        Assert.Equal(exitCode, raw.ExitCode);
        Assert.Contains("bad", raw.StdErr);
    }

    [Fact]
    public async Task Timeout_kills_the_process_and_throws()
    {
        using var transport = CreateTransport();
        var started = Stopwatch.StartNew();

        var ex = await Assert.ThrowsAsync<TvTimeoutException>(() => transport.ExecuteAsync(
            new TvCommand(["hang"], Timeout: TimeSpan.FromMilliseconds(700)),
            TestContext.Current.CancellationToken));

        Assert.True(started.Elapsed < TimeSpan.FromSeconds(10));
        Assert.Equal(TimeSpan.FromMilliseconds(700), ex.Timeout);
    }

    [Fact]
    public async Task Cancellation_throws_operation_canceled()
    {
        using var transport = CreateTransport();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(300);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.ExecuteAsync(TvCommand.Create("hang"), cts.Token));
    }

    [Fact]
    public async Task Commands_are_serialized_when_max_concurrency_is_one()
    {
        using var transport = CreateTransport();
        var first = transport.ExecuteAsync(new TvCommand(["hang"], Timeout: TimeSpan.FromSeconds(1.5)), TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        var watch = Stopwatch.StartNew();

        var second = await transport.ExecuteAsync(TvCommand.Create("echo"), TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(0, second.ExitCode);
        Assert.True(watch.Elapsed > TimeSpan.FromMilliseconds(600), "second command should have waited for the first to time out");
        await Assert.ThrowsAsync<TvTimeoutException>(() => first);
    }

    [Fact]
    public async Task Stream_yields_non_empty_lines()
    {
        using var transport = CreateTransport();
        var lines = new List<string>();

        await foreach (var line in transport.StreamLinesAsync(TvCommand.Create("lines", "3"), TestContext.Current.CancellationToken))
        {
            lines.Add(line);
        }

        Assert.Equal(["""{"n":0}""", """{"n":1}""", """{"n":2}"""], lines);
    }

    [Fact]
    public async Task Stream_does_not_hold_the_command_semaphore()
    {
        using var transport = CreateTransport();
        await using var enumerator = transport
            .StreamLinesAsync(TvCommand.Create("endless", Path.Combine(_dir, "pid-a")), TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await enumerator.MoveNextAsync());

        var raw = await transport.ExecuteAsync(TvCommand.Create("echo"), TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(0, raw.ExitCode);
    }

    [Fact]
    public async Task Stream_failure_surfaces_exit_code_and_cli_message()
    {
        using var transport = CreateTransport();

        var ex = await Assert.ThrowsAsync<TvConnectionException>(async () =>
        {
            await foreach (var unused in transport.StreamLinesAsync(TvCommand.Create("lines-then-fail"), TestContext.Current.CancellationToken))
            {
            }
        });

        Assert.Equal("CDP gone", ex.Message);
    }

    [Fact]
    public async Task Cancelling_or_disposing_a_stream_kills_the_process()
    {
        using var transport = CreateTransport();
        var pidFile = Path.Combine(_dir, "pid-b");
        var enumerator = transport
            .StreamLinesAsync(TvCommand.Create("endless", pidFile), TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await enumerator.MoveNextAsync());
        var pid = int.Parse(await File.ReadAllTextAsync(pidFile, TestContext.Current.CancellationToken));
        using var process = Process.GetProcessById(pid);

        await enumerator.DisposeAsync();

        Assert.True(process.WaitForExit(10_000), "node process should have been killed");
    }

    [Fact]
    public async Task Node_shutdown_crash_after_complete_json_is_treated_as_success()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "0xC0000409 is a Windows exit code.");
        using var transport = CreateTransport();

        var raw = await transport.ExecuteAsync(TvCommand.Create("crash", "{\"success\":true,\"count\":1}"), TestContext.Current.CancellationToken);

        Assert.Equal(0, raw.ExitCode);
        Assert.Contains("count", raw.StdOut);
    }

    [Fact]
    public async Task Node_shutdown_crash_without_json_stays_a_failure()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "0xC0000409 is a Windows exit code.");
        using var transport = CreateTransport();

        var raw = await transport.ExecuteAsync(TvCommand.Create("crash", "half-written {"), TestContext.Current.CancellationToken);

        Assert.Equal(CliProcessTransport.NodeShutdownCrashExitCode, raw.ExitCode);
    }

    [Fact]
    public async Task Missing_executable_is_reported_as_not_found()
    {
        using var transport = new CliProcessTransport(new TvClientOptions
        {
            NodePath = Path.Combine(_dir, "no-such-node.exe"),
            CliScriptPath = _script,
        });

        await Assert.ThrowsAsync<TvCliNotFoundException>(() => transport.ExecuteAsync(TvCommand.Create("echo"), TestContext.Current.CancellationToken));
    }
}
