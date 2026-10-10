namespace TradingView.Client;

/// <summary>Base class for every failure raised by the TradingView client.</summary>
public class TvException : Exception
{
    public TvException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>Process exit code of the <c>tv</c> invocation, when one was produced.</summary>
    public int? ExitCode { get; init; }

    /// <summary>Raw stderr of the <c>tv</c> invocation (JSON or plain text), when available.</summary>
    public string? StdErr { get; init; }
}

/// <summary>The CLI could not reach TradingView over CDP (exit code 2).</summary>
public sealed class TvConnectionException(string message) : TvException(message);

/// <summary>The CLI reported an error (exit code 1), e.g. a bad argument or an unknown command.</summary>
public sealed class TvCommandException(string message) : TvException(message);

/// <summary>The CLI did not finish within the configured timeout and was killed.</summary>
public sealed class TvTimeoutException(string message, TimeSpan timeout) : TvException(message)
{
    public TimeSpan Timeout { get; } = timeout;
}

/// <summary>The CLI produced output that is not valid or not the expected JSON.</summary>
public sealed class TvResponseException(string message, string? stdOut, Exception? innerException = null)
    : TvException(message, innerException)
{
    /// <summary>The unparsed stdout of the invocation.</summary>
    public string? StdOut { get; } = stdOut;
}

/// <summary>The CLI is not installed or could not be located.</summary>
public sealed class TvCliNotFoundException(string message) : TvException(message);

/// <summary>
/// The CLI exited with code 0 but reported <c>success: false</c>.
/// Raised only when <see cref="TvClientOptions.ThrowOnOperationFailure"/> is enabled.
/// </summary>
public sealed class TvOperationFailedException(TvResult result)
    : TvException(result.Error ?? result.Note ?? "The operation reported success: false.")
{
    /// <summary>The typed result that carried <c>success: false</c>.</summary>
    public TvResult Result { get; } = result;
}
