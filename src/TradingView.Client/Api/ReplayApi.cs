using System.Globalization;

namespace TradingView.Client;

internal sealed class ReplayApi(TvExecutor executor) : IReplayApi
{
    public Task<ReplayStartResult> StartAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "replay", "start" };
        if (date is { } d)
        {
            args.Add("--date");
            args.Add(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        return executor.RunAsync(new TvCommand(args), TvJsonContext.Default.ReplayStartResult, cancellationToken);
    }

    public Task<ReplayStepResult> StepAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("replay", "step"), TvJsonContext.Default.ReplayStepResult, cancellationToken);

    public Task<ReplayStopResult> StopAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("replay", "stop"), TvJsonContext.Default.ReplayStopResult, cancellationToken);

    public Task<ReplayStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("replay", "status"), TvJsonContext.Default.ReplayStatusResult, cancellationToken);

    public Task<ReplayAutoplayResult> ToggleAutoplayAsync(ReplaySpeed? speed = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "replay", "autoplay" };
        if (speed is { } s)
        {
            if (!Enum.IsDefined(s))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), s, "Use one of the ReplaySpeed values.");
            }

            args.Add("--speed");
            args.Add(((int)s).ToString(CultureInfo.InvariantCulture));
        }

        return executor.RunAsync(new TvCommand(args), TvJsonContext.Default.ReplayAutoplayResult, cancellationToken);
    }

    public Task<ReplayTradeResult> TradeAsync(ReplayTradeAction action, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown replay action.");
        }

        var command = TvCommand.Create("replay", "trade", action.ToString().ToLowerInvariant());
        return executor.RunAsync(command, TvJsonContext.Default.ReplayTradeResult, cancellationToken);
    }
}
