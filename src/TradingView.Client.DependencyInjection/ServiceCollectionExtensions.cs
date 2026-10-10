using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TradingView.Client.DependencyInjection;

/// <summary>Registration of the TradingView client in a <see cref="IServiceCollection"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITradingViewClient"/> (and its area interfaces such as <see cref="IChartApi"/>) as singletons.
    /// Options come from <see cref="IOptions{TOptions}"/>, so they can also be bound from configuration with
    /// <c>services.Configure&lt;TvClientOptions&gt;(...)</c>. The client owns one transport and is disposed with the container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional options callback.</param>
    public static IServiceCollection AddTradingViewClient(this IServiceCollection services, Action<TvClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<TvClientOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton<ITradingViewClient>(provider => new TradingViewClient(
            provider.GetRequiredService<IOptions<TvClientOptions>>().Value,
            provider.GetService<ILoggerFactory>()));

        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Health);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Chart);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Data);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Pine);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Indicators);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Replay);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Drawings);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Alerts);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Watchlist);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Layouts);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Panes);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Tabs);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Screenshots);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Ui);
        services.TryAddSingleton(provider => provider.GetRequiredService<ITradingViewClient>().Streams);
        return services;
    }
}
