using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TradingView.Client;

/// <summary>
/// Runs the <c>tv</c> CLI as a child process (<c>node index.js ...</c> via <see cref="ProcessStartInfo.ArgumentList"/>,
/// so no shell ever touches the arguments).
/// </summary>
public sealed class CliProcessTransport : ITvTransport, IDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly TvClientOptions _options;
    private readonly TvCliLocator _locator;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _concurrency;

    public CliProcessTransport(TvClientOptions options, ILogger<CliProcessTransport>? logger = null)
        : this(options, new TvCliLocator(options), logger)
    {
    }

    internal CliProcessTransport(TvClientOptions options, TvCliLocator locator, ILogger<CliProcessTransport>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxConcurrency, 1);
        _options = options;
        _locator = locator;
        _logger = logger ?? NullLogger<CliProcessTransport>.Instance;
        _concurrency = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);
    }

    public async Task<TvRawResult> ExecuteAsync(TvCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var timeout = command.Timeout ?? _options.DefaultTimeout;

        await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var process = StartProcess(command);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                // stdout and stderr are drained concurrently, otherwise a full pipe buffer would deadlock the child.
                var stdOutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                var stdErrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
                await WriteStdInAsync(process, command.StdIn, timeoutCts.Token).ConfigureAwait(false);
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                var stdOut = await stdOutTask.ConfigureAwait(false);
                var stdErr = await stdErrTask.ConfigureAwait(false);
                _logger.LogDebug("tv {Command} exited with {ExitCode}", command, process.ExitCode);
                return new TvRawResult(NormalizeExitCode(process.ExitCode, stdOut, command), stdOut, stdErr);
            }
            catch (OperationCanceledException)
            {
                KillQuietly(process);
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                throw new TvTimeoutException($"'tv {command}' did not finish within {timeout.TotalSeconds:0.#} s and was killed.", timeout);
            }
        }
        finally
        {
            _concurrency.Release();
        }
    }

    public async IAsyncEnumerable<string> StreamLinesAsync(
        TvCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        using var process = StartProcess(command);
        var stdErr = new StringBuilder();
        var stdErrPump = PumpStdErrAsync(process, stdErr);

        try
        {
            await WriteStdInAsync(process, command.StdIn, cancellationToken).ConfigureAwait(false);

            while (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (line.Length > 0)
                {
                    yield return line;
                }
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await stdErrPump.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw TvProcessErrors.FromExit(process.ExitCode, stdErr.ToString(), $"tv {command}");
            }
        }
        finally
        {
            // Covers cancellation, enumerator disposal and failures: Windows has no SIGINT, so the tree is killed.
            KillQuietly(process);
        }
    }

    public void Dispose() => _concurrency.Dispose();

    /// <summary>
    /// Windows exit code 0xC0000409 (STATUS_STACK_BUFFER_OVERRUN) that Node produces when libuv asserts at shutdown.
    /// Upstream <c>tv search</c> hits it every time after printing its complete JSON.
    /// </summary>
    internal const int NodeShutdownCrashExitCode = unchecked((int)0xC0000409);

    private int NormalizeExitCode(int exitCode, string stdOut, TvCommand command)
    {
        if (exitCode != NodeShutdownCrashExitCode || string.IsNullOrWhiteSpace(stdOut))
        {
            return exitCode;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(stdOut);
            _logger.LogWarning("tv {Command} crashed while exiting (0xC0000409) after printing complete JSON; treating it as success", command);
            return 0;
        }
        catch (System.Text.Json.JsonException)
        {
            return exitCode;
        }
    }

    private Process StartProcess(TvCommand command)
    {
        var location = _locator.Locate();
        var startInfo = new ProcessStartInfo(location.FileName)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8NoBom,
            StandardOutputEncoding = Utf8NoBom,
            StandardErrorEncoding = Utf8NoBom,
        };

        if (location.WorkingDirectory is { } workingDirectory && Directory.Exists(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var arg in location.PrefixArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (var arg in command.Args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (!string.IsNullOrWhiteSpace(_options.CdpHost))
        {
            startInfo.Environment["TV_CDP_HOST"] = _options.CdpHost;
        }

        if (_options.CdpPort is { } port)
        {
            startInfo.Environment["TV_CDP_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        _logger.LogDebug("Starting {File} {Args}", location.FileName, string.Join(' ', startInfo.ArgumentList));

        try
        {
            return Process.Start(startInfo) ?? throw new TvException($"Could not start '{location.FileName}'.");
        }
        catch (Win32Exception ex)
        {
            throw new TvCliNotFoundException($"Could not start '{location.FileName}': {ex.Message}");
        }
    }

    private static async Task WriteStdInAsync(Process process, string? stdIn, CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrEmpty(stdIn))
            {
                await process.StandardInput.WriteAsync(stdIn.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (IOException)
        {
            // The CLI exited before reading its input; its exit code and stderr tell the story.
        }
        finally
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // Same as above.
            }
        }
    }

    private async Task PumpStdErrAsync(Process process, StringBuilder buffer)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                buffer.AppendLine(line);
                _logger.LogDebug("tv stderr: {Line}", line);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // Process was killed while reading.
        }
    }

    private static void KillQuietly(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already gone.
        }
    }
}

/// <summary>Maps a non-zero CLI exit to the right exception type.</summary>
internal static class TvProcessErrors
{
    public static TvException FromExit(int exitCode, string stdErr, string what)
    {
        var message = TvExecutor.ExtractErrorMessage(stdErr) ?? $"'{what}' failed with exit code {exitCode}.";
        return exitCode switch
        {
            2 => new TvConnectionException(message) { ExitCode = exitCode, StdErr = stdErr },
            1 => new TvCommandException(message) { ExitCode = exitCode, StdErr = stdErr },
            _ => new TvException(message) { ExitCode = exitCode, StdErr = stdErr },
        };
    }
}
