using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace TradingView.Client;

/// <summary>Runs a command and turns the raw outcome into a typed result or a typed exception.</summary>
internal sealed class TvExecutor(ITvTransport transport, TvClientOptions options)
{
    public ITvTransport Transport { get; } = transport;

    public TvClientOptions Options { get; } = options;

    public async Task<T> RunAsync<T>(TvCommand command, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : TvResult
    {
        var raw = await Transport.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        var result = Parse(raw, command, typeInfo);
        if (!result.Success && Options.ThrowOnOperationFailure)
        {
            throw new TvOperationFailedException(result) { ExitCode = raw.ExitCode, StdErr = raw.StdErr };
        }

        return result;
    }

    public static T Parse<T>(TvRawResult raw, TvCommand command, JsonTypeInfo<T> typeInfo)
        where T : TvResult
    {
        if (raw.ExitCode != 0)
        {
            throw TvProcessErrors.FromExit(raw.ExitCode, raw.StdErr, $"tv {command}");
        }

        if (string.IsNullOrWhiteSpace(raw.StdOut))
        {
            throw new TvResponseException($"'tv {command}' produced no output.", raw.StdOut) { ExitCode = raw.ExitCode, StdErr = raw.StdErr };
        }

        try
        {
            return JsonSerializer.Deserialize(raw.StdOut, typeInfo)
                ?? throw new TvResponseException($"'tv {command}' returned JSON null.", raw.StdOut);
        }
        catch (JsonException ex)
        {
            throw new TvResponseException($"'tv {command}' returned unexpected output: {ex.Message}", raw.StdOut, ex) { ExitCode = raw.ExitCode, StdErr = raw.StdErr };
        }
    }

    /// <summary>The CLI writes <c>{ success:false, error }</c> to stderr; unknown commands write plain text.</summary>
    internal static string? ExtractErrorMessage(string stdErr)
    {
        var trimmed = stdErr.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed[0] == '{')
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("error", out var error)
                    && error.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(error.GetString()))
                {
                    return error.GetString();
                }
            }
            catch (JsonException)
            {
                // fall through to the raw text
            }
        }

        return trimmed;
    }
}
