namespace TradingView.Client.IntegrationTests;

public class HealthIntegrationTests
{
    [Fact]
    public async Task Status_reports_a_connected_chart()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = await Live.CreateClientOrSkipAsync(ct);

        var status = await client.Health.GetStatusAsync(ct);

        Assert.True(status.CdpConnected);
        Assert.True(status.ApiAvailable);
        Assert.False(string.IsNullOrEmpty(status.ChartSymbol));
        Assert.Empty(Unmapped(status));
    }

    [Fact]
    public async Task Discover_lists_all_six_probes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = await Live.CreateClientOrSkipAsync(ct);

        var discover = await client.Health.DiscoverAsync(ct);

        Assert.Equal(6, discover.ApisTotal);
        Assert.NotNull(discover.Apis?.ChartApi);
        Assert.NotNull(discover.Apis.ReplayApi);
    }

    [Fact]
    public async Task UiState_describes_the_chart()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = await Live.CreateClientOrSkipAsync(ct);

        var ui = await client.Health.GetUiStateAsync(ct);

        Assert.NotNull(ui.Chart?.Symbol);
        Assert.NotNull(ui.Buttons);
    }

    [Fact]
    public async Task Unreachable_cdp_port_raises_connection_exception()
    {
        var ct = TestContext.Current.CancellationToken;
        using var live = await Live.CreateClientOrSkipAsync(ct); // only meaningful when the CLI itself works
        using var client = new TradingViewClient(new TvClientOptions { CdpPort = 1 });

        await Assert.ThrowsAsync<TvConnectionException>(() => client.Health.GetStatusAsync(ct));
    }

    private static IEnumerable<string> Unmapped(TvObject o)
        => o.AdditionalData?.Keys ?? Enumerable.Empty<string>();
}
