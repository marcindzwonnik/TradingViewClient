namespace TradingView.Client;

/// <summary>Configuration of <see cref="TradingViewClient"/> and the process transport.</summary>
public sealed class TvClientOptions
{
    /// <summary>Explicit path to <c>node</c>. Used together with <see cref="CliScriptPath"/>.</summary>
    public string? NodePath { get; set; }

    /// <summary>Explicit path to the CLI entry point (<c>src/cli/index.js</c> of tradingview-cli).</summary>
    public string? CliScriptPath { get; set; }

    /// <summary>Host of the TradingView CDP endpoint (exported as <c>TV_CDP_HOST</c>).</summary>
    public string? CdpHost { get; set; }

    /// <summary>Port of the TradingView CDP endpoint (exported as <c>TV_CDP_PORT</c>).</summary>
    public int? CdpPort { get; set; }

    /// <summary>
    /// Working directory of the CLI. Defaults to the tradingview-cli checkout (its update check runs <c>git</c>
    /// in the working directory, so a neutral directory would give meaningless results).
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Timeout of a single (non-streaming) command. Defaults to 60 seconds.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum number of commands running at once. Commands share the single active chart,
    /// so the default is 1. Streams do not count against this limit.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// When true (default), a response with exit code 0 and <c>success: false</c> raises
    /// <see cref="TvOperationFailedException"/>. When false, the typed result is returned instead.
    /// </summary>
    public bool ThrowOnOperationFailure { get; set; } = true;

    /// <summary>
    /// When true (default), Pine commands that fail with "Could not open Pine Editor" are retried once after
    /// opening the editor panel (<c>tv ui panel pine-editor open</c>); the CLI is unreliable when the editor is closed.
    /// </summary>
    public bool AutoOpenPineEditor { get; set; } = true;
}
