using System.Runtime.CompilerServices;

namespace TradingView.Client.Tests.Support;

/// <summary>In-memory <see cref="ITvTransport"/> that records commands and replays canned responses.</summary>
internal sealed class FakeTransport : ITvTransport
{
    private readonly Func<TvCommand, TvRawResult> _handler;

    public FakeTransport(Func<TvCommand, TvRawResult> handler) => _handler = handler;

    public FakeTransport(string stdOut, int exitCode = 0, string stdErr = "")
        : this(_ => new TvRawResult(exitCode, stdOut, stdErr))
    {
    }

    public List<TvCommand> Commands { get; } = [];

    public IReadOnlyList<string> LastArgs => Commands[^1].Args;

    public string[] StreamLines { get; set; } = [];

    public Task<TvRawResult> ExecuteAsync(TvCommand command, CancellationToken cancellationToken = default)
    {
        Commands.Add(command);
        return Task.FromResult(_handler(command));
    }

    public async IAsyncEnumerable<string> StreamLinesAsync(
        TvCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Commands.Add(command);
        foreach (var line in StreamLines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return line;
            await Task.Yield();
        }
    }

    public TradingViewClient CreateClient(TvClientOptions? options = null) => new(this, options);
}
