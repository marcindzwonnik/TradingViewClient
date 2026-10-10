using System.Globalization;

namespace TradingView.Client;

internal sealed class HealthApi(TvExecutor executor) : IHealthApi
{
    public Task<StatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("status"), TvJsonContext.Default.StatusResult, cancellationToken);

    public Task<LaunchResult> LaunchAsync(int? port = null, bool killExisting = true, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "launch" };
        if (port is { } p)
        {
            args.Add("--port");
            args.Add(p.ToString(CultureInfo.InvariantCulture));
        }

        if (!killExisting)
        {
            args.Add("--no-kill");
        }

        // Starting the app and waiting for CDP can take a while.
        var command = new TvCommand(args, Timeout: TimeSpan.FromMinutes(2));
        return executor.RunAsync(command, TvJsonContext.Default.LaunchResult, cancellationToken);
    }

    public Task<DiscoverResult> DiscoverAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("discover"), TvJsonContext.Default.DiscoverResult, cancellationToken);

    public Task<UiStateResult> GetUiStateAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("ui-state"), TvJsonContext.Default.UiStateResult, cancellationToken);

    public Task<UpdateResult> UpdateCliAsync(CancellationToken cancellationToken = default)
    {
        var command = new TvCommand(["update"], Timeout: TimeSpan.FromMinutes(5));
        return executor.RunAsync(command, TvJsonContext.Default.UpdateResult, cancellationToken);
    }
}
