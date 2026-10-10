namespace TradingView.Client;

/// <summary>Kind of script created by <c>tv pine new</c>.</summary>
public enum PineScriptType
{
    Indicator,
    Strategy,
    Library,
}

/// <summary>Order sent in replay mode (<c>tv replay trade</c>).</summary>
public enum ReplayTradeAction
{
    Buy,
    Sell,
    Close,
}

/// <summary>
/// Replay autoplay delay in milliseconds (lower = faster). TradingView accepts only these values;
/// the enum value is the delay passed to the CLI.
/// </summary>
public enum ReplaySpeed
{
    Ms100 = 100,
    Ms143 = 143,
    Ms200 = 200,
    Ms300 = 300,
    Ms1000 = 1000,
    Ms2000 = 2000,
    Ms3000 = 3000,
    Ms5000 = 5000,
    Ms10000 = 10000,
}

/// <summary>Severity of a Pine compiler marker (Monaco marker severities, as in <see cref="PineMarker.Severity"/>).</summary>
public enum PineMarkerSeverity
{
    Unknown = 0,
    Hint = 1,
    Info = 2,
    Warning = 4,
    Error = 8,
}

/// <summary>Severity of a <c>tv pine analyze</c> diagnostic.</summary>
public enum PineDiagnosticSeverity
{
    Unknown = 0,
    Error,
    Warning,
    Info,
}

/// <summary>Kind of a Pine console entry.</summary>
public enum PineConsoleEntryType
{
    Unknown = 0,
    Info,
    Error,
    Compile,
    Warning,
}
