using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradingView.Client.DependencyInjection;
using TradingView.Client.Tests.Support;

namespace TradingView.Client.Tests;

public class Stage5Tests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Json_error_after_progress_lines_becomes_the_exception_message()
    {
        var stderr = "[stream:ohlcv] stopped. Provisioned panes and tabs were left open.\n{\n  \"success\": false,\n  \"error\": \"Timed out setting symbol for NASDAQ:AAPL@15.\"\n}\n";
        var transport = new FakeTransport("", exitCode: 1, stdErr: stderr);
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvCommandException>(() => client.Health.GetStatusAsync(Ct));

        Assert.Equal("Timed out setting symbol for NASDAQ:AAPL@15.", ex.Message);
        Assert.Equal(stderr, ex.StdErr);
    }

    [Theory]
    [InlineData("progress line\nnot json at all")]
    [InlineData("progress\n{ broken")]
    [InlineData("progress\n{\"no_error\": true}")]
    public async Task Stderr_without_a_usable_json_error_is_kept_as_text(string stderr)
    {
        var transport = new FakeTransport("", exitCode: 1, stdErr: stderr);
        using var client = transport.CreateClient();

        var ex = await Assert.ThrowsAsync<TvCommandException>(() => client.Health.GetStatusAsync(Ct));

        Assert.Equal(stderr, ex.Message);
    }

    // --- Dependency injection -----------------------------------------------------------------------------

    [Fact]
    public void AddTradingViewClient_registers_the_client_and_every_area_as_singletons()
    {
        var services = new ServiceCollection();
        services.AddTradingViewClient(o => o.CdpPort = 9333);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<ITradingViewClient>();

        Assert.Same(client, provider.GetRequiredService<ITradingViewClient>());
        Assert.Same(client.Health, provider.GetRequiredService<IHealthApi>());
        Assert.Same(client.Chart, provider.GetRequiredService<IChartApi>());
        Assert.Same(client.Data, provider.GetRequiredService<IDataApi>());
        Assert.Same(client.Pine, provider.GetRequiredService<IPineApi>());
        Assert.Same(client.Indicators, provider.GetRequiredService<IIndicatorApi>());
        Assert.Same(client.Replay, provider.GetRequiredService<IReplayApi>());
        Assert.Same(client.Drawings, provider.GetRequiredService<IDrawingApi>());
        Assert.Same(client.Alerts, provider.GetRequiredService<IAlertApi>());
        Assert.Same(client.Watchlist, provider.GetRequiredService<IWatchlistApi>());
        Assert.Same(client.Layouts, provider.GetRequiredService<ILayoutApi>());
        Assert.Same(client.Panes, provider.GetRequiredService<IPaneApi>());
        Assert.Same(client.Tabs, provider.GetRequiredService<ITabApi>());
        Assert.Same(client.Screenshots, provider.GetRequiredService<IScreenshotApi>());
        Assert.Same(client.Ui, provider.GetRequiredService<IUiApi>());
        Assert.Same(client.Streams, provider.GetRequiredService<IStreamApi>());
    }

    [Fact]
    public void Client_interface_exposes_exactly_the_registered_areas()
    {
        var areas = typeof(ITradingViewClient).GetProperties().Select(p => p.PropertyType).Where(t => t.Name.EndsWith("Api", StringComparison.Ordinal)).ToHashSet();
        var services = new ServiceCollection();
        services.AddTradingViewClient();

        foreach (var area in areas)
        {
            Assert.Contains(services, d => d.ServiceType == area);
        }

        Assert.Equal(15, areas.Count);
    }

    [Fact]
    public void Options_callback_and_options_binding_are_both_honoured()
    {
        var services = new ServiceCollection();
        services.AddTradingViewClient(o => o.CdpHost = "10.0.0.5");
        services.Configure<TvClientOptions>(o => o.DefaultTimeout = TimeSpan.FromSeconds(7));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<TvClientOptions>>().Value;

        Assert.Equal("10.0.0.5", options.CdpHost);
        Assert.Equal(TimeSpan.FromSeconds(7), options.DefaultTimeout);
        Assert.True(options.ThrowOnOperationFailure);
    }

    [Fact]
    public void An_existing_registration_is_not_replaced()
    {
        var custom = new TradingViewClient(new FakeTransport(""));
        var services = new ServiceCollection();
        services.AddSingleton<ITradingViewClient>(custom);
        services.AddTradingViewClient();
        using var provider = services.BuildServiceProvider();

        Assert.Same(custom, provider.GetRequiredService<ITradingViewClient>());
    }

    [Fact]
    public void AddTradingViewClient_rejects_a_null_collection()
        => Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddTradingViewClient(null!));

    [Fact]
    public async Task The_resolved_client_works_when_the_cli_is_missing_with_a_helpful_error()
    {
        var services = new ServiceCollection();
        services.AddTradingViewClient(o =>
        {
            o.CliScriptPath = Path.Combine(Path.GetTempPath(), "definitely-not-there", "index.js");
        });
        await using var provider = services.BuildServiceProvider();

        var ex = await Assert.ThrowsAsync<TvCliNotFoundException>(() => provider.GetRequiredService<IHealthApi>().GetStatusAsync(Ct));

        Assert.Contains("CliScriptPath", ex.Message);
    }
}
