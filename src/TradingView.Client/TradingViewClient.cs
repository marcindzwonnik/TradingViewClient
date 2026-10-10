using Microsoft.Extensions.Logging;

namespace TradingView.Client;

/// <summary>Entry point: one property per area of the <c>tv</c> CLI.</summary>
public interface ITradingViewClient : IDisposable
{
    IHealthApi Health { get; }

    IChartApi Chart { get; }

    IDataApi Data { get; }
}

/// <inheritdoc cref="ITradingViewClient"/>
public sealed class TradingViewClient : ITradingViewClient
{
    private readonly CliProcessTransport? _ownedTransport;

    /// <summary>Creates a client that runs the real <c>tv</c> CLI as a child process.</summary>
    public TradingViewClient(TvClientOptions? options = null, ILoggerFactory? loggerFactory = null)
        : this(options ?? new TvClientOptions(), loggerFactory, transport: null)
    {
    }

    /// <summary>Creates a client over a custom transport (tests, remote execution).</summary>
    public TradingViewClient(ITvTransport transport, TvClientOptions? options = null)
        : this(options ?? new TvClientOptions(), loggerFactory: null, transport)
    {
    }

    private TradingViewClient(TvClientOptions options, ILoggerFactory? loggerFactory, ITvTransport? transport)
    {
        if (transport is null)
        {
            _ownedTransport = new CliProcessTransport(options, loggerFactory?.CreateLogger<CliProcessTransport>());
            transport = _ownedTransport;
        }

        var executor = new TvExecutor(transport, options);
        Health = new HealthApi(executor);
        Chart = new ChartApi(executor);
        Data = new DataApi(executor);
    }

    public IHealthApi Health { get; }

    public IChartApi Chart { get; }

    public IDataApi Data { get; }

    public void Dispose() => _ownedTransport?.Dispose();
}
