namespace TradingView.Client;

/// <summary>Executes <c>tv</c> commands. Implemented over a child process; replaceable in tests.</summary>
public interface ITvTransport
{
    /// <summary>Runs a command to completion and returns its raw output.</summary>
    Task<TvRawResult> ExecuteAsync(TvCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a long-lived command (<c>stream *</c>) and yields each non-empty stdout line.
    /// Disposing the enumerator or cancelling stops the process.
    /// </summary>
    IAsyncEnumerable<string> StreamLinesAsync(TvCommand command, CancellationToken cancellationToken = default);
}
