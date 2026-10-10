using TradingView.Client.Tests.Support;

namespace TradingView.Client.Tests;

public class HealthApiTests
{
    private static FakeTransport RespondWith(string fixture) => new(Fixtures.Read(fixture + ".json"));

    [Fact]
    public async Task GetStatus_runs_status_and_parses_fixture()
    {
        var transport = RespondWith("health/status");
        using var client = transport.CreateClient();

        var status = await client.Health.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["status"], transport.LastArgs);
        Assert.True(status.CdpConnected);
    }

    [Fact]
    public async Task Discover_and_UiState_use_their_commands()
    {
        var transport = new FakeTransport(cmd => new TvRawResult(0, Fixtures.Read($"health/{cmd.Args[0]}.json"), ""));
        using var client = transport.CreateClient();

        await client.Health.DiscoverAsync(TestContext.Current.CancellationToken);
        await client.Health.GetUiStateAsync(TestContext.Current.CancellationToken);

        Assert.Equal([["discover"], ["ui-state"]], transport.Commands.Select(c => c.Args.ToArray()));
    }

    public static TheoryData<int?, bool, string[]> LaunchCases() => new()
    {
        { null, true, ["launch"] },
        { 9333, true, ["launch", "--port", "9333"] },
        { null, false, ["launch", "--no-kill"] },
        { 9333, false, ["launch", "--port", "9333", "--no-kill"] },
    };

    [Theory]
    [MemberData(nameof(LaunchCases))]
    public async Task Launch_builds_arguments(int? port, bool killExisting, string[] expected)
    {
        var transport = new FakeTransport("""{"success":true,"platform":"win32","binary":"tv.exe","pid":1,"cdp_port":9222,"cdp_ready":false,"warning":"slow"}""");
        using var client = transport.CreateClient();

        var launch = await client.Health.LaunchAsync(port, killExisting, TestContext.Current.CancellationToken);

        Assert.Equal(expected, transport.LastArgs);
        Assert.Equal(false, launch.CdpReady);
        Assert.Equal("slow", launch.Warning);
        Assert.True(transport.Commands[0].Timeout > TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Launch_ready_response_has_no_cdp_ready_flag()
    {
        var transport = new FakeTransport("""{"success":true,"platform":"win32","binary":"x","pid":7,"cdp_port":9222,"cdp_url":"http://localhost:9222","browser":"b","user_agent":"u"}""");
        using var client = transport.CreateClient();

        var launch = await client.Health.LaunchAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(launch.CdpReady);
        Assert.Equal(7, launch.Pid);
    }

    [Fact]
    public async Task Update_up_to_date_parses()
    {
        var transport = new FakeTransport("""{"success":true,"updated":false,"status":"up_to_date","commit":"abc"}""");
        using var client = transport.CreateClient();

        var update = await client.Health.UpdateCliAsync(TestContext.Current.CancellationToken);

        Assert.False(update.Updated);
        Assert.Equal("up_to_date", update.Status);
    }

    [Fact]
    public async Task Update_soft_failure_throws_operation_failed_with_typed_result()
    {
        var transport = new FakeTransport("""{"success":false,"error":"Working tree has local changes","changed_files":["a.js","b.js"]}""");
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvOperationFailedException>(() => client.Health.UpdateCliAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Working tree has local changes", ex.Message);
        var result = Assert.IsType<UpdateResult>(ex.Result);
        Assert.Equal(["a.js", "b.js"], result.ChangedFiles);
    }

    [Fact]
    public async Task Update_soft_failure_is_returned_when_throwing_is_disabled()
    {
        var transport = new FakeTransport("""{"success":false,"error":"offline"}""");
        using var client = transport.CreateClient(new TvClientOptions { ThrowOnOperationFailure = false });

        var update = await client.Health.UpdateCliAsync(TestContext.Current.CancellationToken);

        Assert.False(update.Success);
        Assert.Equal("offline", update.Error);
    }

    [Fact]
    public async Task Exit_code_2_maps_to_connection_exception_with_cli_message()
    {
        var transport = new FakeTransport("", exitCode: 2, stdErr: """{"success":false,"error":"CDP connection refused"}""");
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvConnectionException>(() => client.Health.GetStatusAsync(TestContext.Current.CancellationToken));

        Assert.Equal("CDP connection refused", ex.Message);
        Assert.Equal(2, ex.ExitCode);
    }

    [Fact]
    public async Task Exit_code_1_with_json_error_maps_to_command_exception()
    {
        var transport = new FakeTransport("", exitCode: 1, stdErr: """{"success":false,"error":"Bad thing"}""");
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvCommandException>(() => client.Health.GetStatusAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Bad thing", ex.Message);
    }

    [Fact]
    public async Task Exit_code_1_with_plain_text_stderr_keeps_the_text()
    {
        var transport = new FakeTransport("", exitCode: 1, stdErr: "Unknown command: foo\n");
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvCommandException>(() => client.Health.GetStatusAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Unknown command: foo", ex.Message);
        Assert.Equal("Unknown command: foo\n", ex.StdErr);
    }

    [Fact]
    public async Task Exit_code_1_with_empty_stderr_still_has_a_message()
    {
        var transport = new FakeTransport("", exitCode: 1);
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvCommandException>(() => client.Health.GetStatusAsync(TestContext.Current.CancellationToken));

        Assert.Contains("exit code 1", ex.Message);
    }

    [Fact]
    public async Task Unexpected_exit_code_maps_to_base_exception()
    {
        var transport = new FakeTransport("", exitCode: 137, stdErr: "killed");
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvException>(() => client.Health.GetStatusAsync(TestContext.Current.CancellationToken));

        Assert.Equal(typeof(TvException), ex.GetType());
        Assert.Equal(137, ex.ExitCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n")]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    public async Task Invalid_stdout_maps_to_response_exception(string stdout)
    {
        var transport = new FakeTransport(stdout);
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvResponseException>(() => client.Health.GetStatusAsync(TestContext.Current.CancellationToken));

        Assert.Equal(stdout, ex.StdOut);
    }
}
