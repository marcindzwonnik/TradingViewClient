using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>A compiler marker in the Pine editor (Monaco marker).</summary>
public sealed record PineMarker : TvObject
{
    public int Line { get; init; }
    public int Column { get; init; }
    public string Message { get; init; } = string.Empty;

    /// <summary>Raw Monaco severity: 1 hint, 2 info, 4 warning, 8 error.</summary>
    public int Severity { get; init; }

    [JsonIgnore]
    public PineMarkerSeverity Level => Enum.IsDefined((PineMarkerSeverity)Severity) ? (PineMarkerSeverity)Severity : PineMarkerSeverity.Unknown;
}

/// <summary>Result of <c>tv pine get</c>. The source can be large (hundreds of KB for big scripts).</summary>
public sealed record PineSourceResult : TvResult
{
    public string Source { get; init; } = string.Empty;
    public int LineCount { get; init; }
    public int CharCount { get; init; }
}

/// <summary>Result of <c>tv pine set</c>.</summary>
public sealed record PineSetSourceResult : TvResult
{
    public int LinesSet { get; init; }
}

/// <summary>
/// Result of <c>tv pine compile</c>. <see cref="TvResult.Success"/> is true even when the script has compile errors;
/// check <see cref="HasErrors"/>. For a new, unsaved script TradingView opens a "Save script" dialog first
/// (<see cref="ButtonClicked"/> is then "Pine Save" and <see cref="StudyAdded"/> false).
/// </summary>
public sealed record PineCompileResult : TvResult
{
    /// <summary>"Save and add to chart", "Add to chart", "Update on chart", "Pine Save" or "keyboard_shortcut".</summary>
    public string? ButtonClicked { get; init; }

    public bool HasErrors { get; init; }
    public IReadOnlyList<PineMarker> Errors { get; init; } = [];

    /// <summary>True when the script was added to / updated on the chart; null when it could not be determined.</summary>
    public bool? StudyAdded { get; init; }
}

/// <summary>Result of <c>tv pine raw-compile</c> (clicks the compile button without any checks).</summary>
public sealed record PineRawCompileResult : TvResult
{
    public string? ButtonClicked { get; init; }
    public string? Source { get; init; }
}

/// <summary>A finding of the offline static analysis.</summary>
public sealed record PineDiagnostic : TvObject
{
    public int Line { get; init; }
    public int Column { get; init; }
    public string Message { get; init; } = string.Empty;

    [JsonConverter(typeof(TolerantEnumConverter<PineDiagnosticSeverity>))]
    public PineDiagnosticSeverity Severity { get; init; }
}

/// <summary>Result of <c>tv pine analyze</c> (offline; no TradingView needed).</summary>
public sealed record PineAnalyzeResult : TvResult
{
    public int IssueCount { get; init; }
    public IReadOnlyList<PineDiagnostic> Diagnostics { get; init; } = [];
}

/// <summary>An error or warning from the server-side compile check.</summary>
public sealed record PineCheckIssue : TvObject
{
    public int? Line { get; init; }
    public int? Column { get; init; }
    public int? EndLine { get; init; }
    public int? EndColumn { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>Result of <c>tv pine check</c>. <see cref="TvResult.Success"/> is true even when <see cref="Compiled"/> is false.</summary>
public sealed record PineCheckResult : TvResult
{
    public bool Compiled { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public IReadOnlyList<PineCheckIssue>? Errors { get; init; }
    public IReadOnlyList<PineCheckIssue>? Warnings { get; init; }
}

/// <summary>Result of <c>tv pine save</c>.</summary>
public sealed record PineSaveResult : TvResult
{
    /// <summary>"saved_with_dialog" or "Ctrl+S_dispatched".</summary>
    public string? Action { get; init; }
}

/// <summary>Result of <c>tv pine new</c>. The first call fails with "Could not open Pine Editor" when the editor is closed; open it (<c>IUiApi.PanelAsync</c>) and retry.</summary>
public sealed record PineNewResult : TvResult
{
    public string? Type { get; init; }
    public string? Action { get; init; }
    public string? Template { get; init; }
}

/// <summary>Result of <c>tv pine open NAME</c>.</summary>
public sealed record PineOpenResult : TvResult
{
    public string Name { get; init; } = string.Empty;
    public string ScriptId { get; init; } = string.Empty;
    public int Lines { get; init; }
    public string? Source { get; init; }
    public bool Opened { get; init; }
}

/// <summary>A saved Pine script of the account.</summary>
public sealed record PineScriptInfo : TvObject
{
    public string? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Title { get; init; }

    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Version { get; init; }

    /// <summary>Last modification as reported by TradingView (unix seconds in practice).</summary>
    public JsonElement? Modified { get; init; }

    /// <summary><see cref="Modified"/> as a timestamp, or null when it is absent or not a number.</summary>
    [JsonIgnore]
    public DateTimeOffset? ModifiedAt
        => Modified is { ValueKind: JsonValueKind.Number } m && m.TryGetInt64(out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
}

/// <summary>Result of <c>tv pine list</c>. A partial result carries <see cref="TvResult.Error"/> with <c>success: true</c>.</summary>
public sealed record PineListResult : TvResult
{
    public IReadOnlyList<PineScriptInfo> Scripts { get; init; } = [];
    public int Count { get; init; }
    public string? Source { get; init; }
}

/// <summary>Result of <c>tv pine errors</c>.</summary>
public sealed record PineErrorsResult : TvResult
{
    public bool HasErrors { get; init; }
    public int ErrorCount { get; init; }
    public IReadOnlyList<PineMarker> Errors { get; init; } = [];
}

/// <summary>An entry of the Pine console panel. TradingView renders it as one text block, so <see cref="Message"/> can contain a whole panel dump.</summary>
public sealed record PineConsoleEntry : TvObject
{
    /// <summary>"HH:MM:SS" or "YYYY-MM-DD HH:MM:SS"; frequently null.</summary>
    public string? Timestamp { get; init; }

    [JsonConverter(typeof(TolerantEnumConverter<PineConsoleEntryType>))]
    public PineConsoleEntryType Type { get; init; }

    public string Message { get; init; } = string.Empty;
}

/// <summary>Result of <c>tv pine console</c>.</summary>
public sealed record PineConsoleResult : TvResult
{
    public IReadOnlyList<PineConsoleEntry> Entries { get; init; } = [];
    public int EntryCount { get; init; }
}
