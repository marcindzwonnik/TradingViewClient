namespace TradingView.Client;

/// <summary>Bar replay (<c>tv replay *</c>). Replay changes the chart until <see cref="StopAsync"/> is called.</summary>
public interface IReplayApi
{
    /// <summary>Starts replay at <paramref name="date"/>, or at the first available date when null.</summary>
    /// <exception cref="TvCommandException">Replay is unavailable for the symbol/timeframe or the date has no data.</exception>
    Task<ReplayStartResult> StartAsync(DateOnly? date = null, CancellationToken cancellationToken = default);

    /// <exception cref="TvCommandException">Replay is not started.</exception>
    Task<ReplayStepResult> StepAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops replay and returns to realtime. Safe to call when replay is not running.</summary>
    Task<ReplayStopResult> StopAsync(CancellationToken cancellationToken = default);

    Task<ReplayStatusResult> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Turns autoplay on or off (it is a toggle); <paramref name="speed"/> changes the delay first.</summary>
    /// <exception cref="TvCommandException">Replay is not started.</exception>
    Task<ReplayAutoplayResult> ToggleAutoplayAsync(ReplaySpeed? speed = null, CancellationToken cancellationToken = default);

    /// <exception cref="TvCommandException">Replay is not started.</exception>
    Task<ReplayTradeResult> TradeAsync(ReplayTradeAction action, CancellationToken cancellationToken = default);
}
