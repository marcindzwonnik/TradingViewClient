namespace TradingView.Client;

/// <summary>Pine Script editor, compiler and saved scripts (<c>tv pine *</c>). Source text always travels over stdin.</summary>
public interface IPineApi
{
    /// <summary>Source currently in the Pine editor. Can be very large.</summary>
    Task<PineSourceResult> GetSourceAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the editor content. The editor is opened by the CLI if needed.</summary>
    Task<PineSetSourceResult> SetSourceAsync(string source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clicks the right compile button for the script and reports editor errors. Compile errors do not throw:
    /// inspect <see cref="PineCompileResult.HasErrors"/>. A new unsaved script makes TradingView open a "Save script" dialog.
    /// </summary>
    Task<PineCompileResult> CompileAsync(CancellationToken cancellationToken = default);

    /// <summary>Clicks the compile / add button without any detection or error check.</summary>
    Task<PineRawCompileResult> RawCompileAsync(CancellationToken cancellationToken = default);

    /// <summary>Offline static analysis of <paramref name="source"/>; needs no TradingView connection for the analysis itself.</summary>
    Task<PineAnalyzeResult> AnalyzeAsync(string source, CancellationToken cancellationToken = default);

    /// <summary>Server-side compile check of <paramref name="source"/> (no chart needed). A failing script returns <see cref="PineCheckResult.Compiled"/> false instead of throwing.</summary>
    Task<PineCheckResult> CheckAsync(string source, CancellationToken cancellationToken = default);

    /// <summary>Saves the current script (Ctrl+S). Creates or overwrites a saved script in the account.</summary>
    Task<PineSaveResult> SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the editor content with a blank template. Fails with <see cref="TvCommandException"/> when the Pine editor is closed.</summary>
    Task<PineNewResult> NewAsync(PineScriptType type = PineScriptType.Indicator, CancellationToken cancellationToken = default);

    /// <summary>Opens a saved script by name.</summary>
    Task<PineOpenResult> OpenAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Lists saved scripts.</summary>
    Task<PineListResult> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Compiler markers currently shown in the editor.</summary>
    Task<PineErrorsResult> GetErrorsAsync(CancellationToken cancellationToken = default);

    /// <summary>Text of the Pine console / log panel.</summary>
    Task<PineConsoleResult> GetConsoleAsync(CancellationToken cancellationToken = default);
}
