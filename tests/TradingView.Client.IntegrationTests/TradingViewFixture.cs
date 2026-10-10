namespace TradingView.Client.IntegrationTests;

/// <summary>
/// Shared live-TradingView plumbing. Tests skip (not fail) when the CDP endpoint is unreachable.
/// Tests that change state additionally require <c>TV_INTEGRATION_MUTATING=1</c>.
/// </summary>
public static class Live
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

    public static string CdpHost => Environment.GetEnvironmentVariable("TV_CDP_HOST") ?? "127.0.0.1";

    public static int CdpPort => int.TryParse(Environment.GetEnvironmentVariable("TV_CDP_PORT"), out var port) ? port : 9222;

    public static bool MutatingEnabled => Environment.GetEnvironmentVariable("TV_INTEGRATION_MUTATING") == "1";

    /// <summary>Creates a client, or skips the current test when TradingView is not reachable over CDP.</summary>
    public static async Task<TradingViewClient> CreateClientOrSkipAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync($"http://{CdpHost}:{CdpPort}/json/version", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Assert.Skip($"TradingView is not reachable over CDP at {CdpHost}:{CdpPort}: {ex.Message}");
        }

        return new TradingViewClient(new TvClientOptions { CdpHost = CdpHost, CdpPort = CdpPort });
    }

    public static void SkipUnlessMutating()
        => Assert.SkipUnless(MutatingEnabled, "Mutating test: set TV_INTEGRATION_MUTATING=1 to run it.");
}
