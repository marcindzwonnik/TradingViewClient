namespace TradingView.Client;

internal sealed class PineApi(TvExecutor executor) : IPineApi
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan EditorOpenDelay = TimeSpan.FromSeconds(2);

    public Task<PineSourceResult> GetSourceAsync(CancellationToken cancellationToken = default)
        => RunEditor(TvCommand.Create("pine", "get"), TvJsonContext.Default.PineSourceResult, cancellationToken);

    public Task<PineSetSourceResult> SetSourceAsync(string source, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return RunEditor(WithSource("set", source), TvJsonContext.Default.PineSetSourceResult, cancellationToken);
    }

    public Task<PineCompileResult> CompileAsync(CancellationToken cancellationToken = default)
        => RunEditor(new TvCommand(["pine", "compile"], Timeout: CompileTimeout), TvJsonContext.Default.PineCompileResult, cancellationToken);

    public Task<PineRawCompileResult> RawCompileAsync(CancellationToken cancellationToken = default)
        => RunEditor(new TvCommand(["pine", "raw-compile"], Timeout: CompileTimeout), TvJsonContext.Default.PineRawCompileResult, cancellationToken);

    public Task<PineAnalyzeResult> AnalyzeAsync(string source, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return executor.RunAsync(WithSource("analyze", source), TvJsonContext.Default.PineAnalyzeResult, cancellationToken);
    }

    public Task<PineCheckResult> CheckAsync(string source, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return executor.RunAsync(WithSource("check", source), TvJsonContext.Default.PineCheckResult, cancellationToken);
    }

    public Task<PineSaveResult> SaveAsync(CancellationToken cancellationToken = default)
        => RunEditor(TvCommand.Create("pine", "save"), TvJsonContext.Default.PineSaveResult, cancellationToken);

    public Task<PineNewResult> NewAsync(PineScriptType type = PineScriptType.Indicator, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown script type.");
        }

        var wire = type.ToString().ToLowerInvariant();
        return RunEditor(TvCommand.Create("pine", "new", wire), TvJsonContext.Default.PineNewResult, cancellationToken);
    }

    public Task<PineOpenResult> OpenAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return executor.RunAsync(TvCommand.Create("pine", "open", name), TvJsonContext.Default.PineOpenResult, cancellationToken);
    }

    public Task<PineListResult> ListAsync(CancellationToken cancellationToken = default)
        => executor.RunAsync(TvCommand.Create("pine", "list"), TvJsonContext.Default.PineListResult, cancellationToken);

    public Task<PineErrorsResult> GetErrorsAsync(CancellationToken cancellationToken = default)
        => RunEditor(TvCommand.Create("pine", "errors"), TvJsonContext.Default.PineErrorsResult, cancellationToken);

    public Task<PineConsoleResult> GetConsoleAsync(CancellationToken cancellationToken = default)
        => RunEditor(TvCommand.Create("pine", "console"), TvJsonContext.Default.PineConsoleResult, cancellationToken);

    /// <summary>Runs a command that needs the Pine editor; opens the editor and retries once when the CLI could not.</summary>
    private async Task<T> RunEditor<T>(TvCommand command, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : TvResult
    {
        try
        {
            return await executor.RunAsync(command, typeInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (TvCommandException ex) when (executor.Options.AutoOpenPineEditor && ex.Message.Contains("Pine Editor", StringComparison.OrdinalIgnoreCase))
        {
            await executor.Transport.ExecuteAsync(TvCommand.Create("ui", "panel", "pine-editor", "open"), cancellationToken).ConfigureAwait(false);
            await Task.Delay(EditorOpenDelay, cancellationToken).ConfigureAwait(false);
            return await executor.RunAsync(command, typeInfo, cancellationToken).ConfigureAwait(false);
        }
    }

    private static TvCommand WithSource(string subCommand, string source) => new(["pine", subCommand], StdIn: source);
}
