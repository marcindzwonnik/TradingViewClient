namespace TradingView.Client;

/// <summary>A single invocation of the <c>tv</c> CLI.</summary>
/// <param name="Args">Arguments after the executable, e.g. <c>["quote", "NASDAQ:AAPL"]</c>.</param>
/// <param name="StdIn">Text written to the process standard input (e.g. Pine source), or null.</param>
/// <param name="Timeout">Overrides <see cref="TvClientOptions.DefaultTimeout"/> for this command.</param>
public sealed record TvCommand(IReadOnlyList<string> Args, string? StdIn = null, TimeSpan? Timeout = null)
{
    public static TvCommand Create(params string[] args) => new(args);

    public override string ToString() => string.Join(' ', Args);
}

/// <summary>The unprocessed outcome of a <c>tv</c> invocation.</summary>
public sealed record TvRawResult(int ExitCode, string StdOut, string StdErr);
