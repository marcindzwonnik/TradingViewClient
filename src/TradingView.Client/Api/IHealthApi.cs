namespace TradingView.Client;

/// <summary>Connection, launch, discovery and UI state (<c>tv status|launch|discover|ui-state|update</c>).</summary>
public interface IHealthApi
{
    /// <summary>Checks the CDP connection and returns basic chart info.</summary>
    /// <exception cref="TvConnectionException">TradingView is not reachable over CDP.</exception>
    Task<StatusResult> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Launches TradingView Desktop with CDP enabled.</summary>
    /// <param name="port">CDP port (CLI default 9222).</param>
    /// <param name="killExisting">Kill running TradingView instances first (CLI default); false passes <c>--no-kill</c>.</param>
    Task<LaunchResult> LaunchAsync(int? port = null, bool killExisting = true, CancellationToken cancellationToken = default);

    /// <summary>Probes which internal TradingView APIs are reachable.</summary>
    Task<DiscoverResult> DiscoverAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns panel visibility, visible buttons and chart/replay summary.</summary>
    Task<UiStateResult> GetUiStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates the upstream tradingview-cli checkout (git fast-forward + npm ci).</summary>
    /// <exception cref="TvOperationFailedException">The update was refused (dirty tree, not on main, offline, ...).</exception>
    Task<UpdateResult> UpdateCliAsync(CancellationToken cancellationToken = default);
}
