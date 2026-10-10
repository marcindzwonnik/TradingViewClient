using System.Text.Json.Serialization;

namespace TradingView.Client;

/// <summary>Source-generated (AOT friendly) JSON metadata for every CLI response model.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
// Health
[JsonSerializable(typeof(StatusResult))]
[JsonSerializable(typeof(LaunchResult))]
[JsonSerializable(typeof(UpdateResult))]
[JsonSerializable(typeof(DiscoverResult))]
[JsonSerializable(typeof(UiStateResult))]
// Chart
[JsonSerializable(typeof(ChartStateResult))]
[JsonSerializable(typeof(SymbolResult))]
[JsonSerializable(typeof(SetSymbolResult))]
[JsonSerializable(typeof(TimeframeResult))]
[JsonSerializable(typeof(SetTimeframeResult))]
[JsonSerializable(typeof(ChartTypeResult))]
[JsonSerializable(typeof(SymbolInfoResult))]
[JsonSerializable(typeof(SymbolSearchResult))]
[JsonSerializable(typeof(VisibleRangeResult))]
[JsonSerializable(typeof(SetVisibleRangeResult))]
[JsonSerializable(typeof(ScrollResult))]
// Data
[JsonSerializable(typeof(QuoteResult))]
[JsonSerializable(typeof(OhlcvResult))]
[JsonSerializable(typeof(OhlcvSummaryResult))]
[JsonSerializable(typeof(StudyValuesResult))]
[JsonSerializable(typeof(PineLinesResult))]
[JsonSerializable(typeof(PineLabelsResult))]
[JsonSerializable(typeof(PineTablesResult))]
[JsonSerializable(typeof(PineBoxesResult))]
[JsonSerializable(typeof(StrategyResultsResult))]
[JsonSerializable(typeof(TradesResult))]
[JsonSerializable(typeof(EquityResult))]
[JsonSerializable(typeof(DepthResult))]
[JsonSerializable(typeof(IndicatorInfoResult))]
// Pine
[JsonSerializable(typeof(PineSourceResult))]
[JsonSerializable(typeof(PineSetSourceResult))]
[JsonSerializable(typeof(PineCompileResult))]
[JsonSerializable(typeof(PineRawCompileResult))]
[JsonSerializable(typeof(PineAnalyzeResult))]
[JsonSerializable(typeof(PineCheckResult))]
[JsonSerializable(typeof(PineSaveResult))]
[JsonSerializable(typeof(PineNewResult))]
[JsonSerializable(typeof(PineOpenResult))]
[JsonSerializable(typeof(PineListResult))]
[JsonSerializable(typeof(PineErrorsResult))]
[JsonSerializable(typeof(PineConsoleResult))]
// Indicators
[JsonSerializable(typeof(IndicatorAddResult))]
[JsonSerializable(typeof(IndicatorRemoveResult))]
[JsonSerializable(typeof(IndicatorVisibilityResult))]
[JsonSerializable(typeof(IndicatorSetInputsResult))]
// Replay
[JsonSerializable(typeof(ReplayStartResult))]
[JsonSerializable(typeof(ReplayStepResult))]
[JsonSerializable(typeof(ReplayStopResult))]
[JsonSerializable(typeof(ReplayStatusResult))]
[JsonSerializable(typeof(ReplayAutoplayResult))]
[JsonSerializable(typeof(ReplayTradeResult))]
// Drawing
[JsonSerializable(typeof(DrawShapeResult))]
[JsonSerializable(typeof(DrawingListResult))]
[JsonSerializable(typeof(DrawingDetailsResult))]
[JsonSerializable(typeof(DrawingRemoveResult))]
[JsonSerializable(typeof(DrawingClearResult))]
// Alerts
[JsonSerializable(typeof(AlertListResult))]
[JsonSerializable(typeof(AlertCreateResult))]
[JsonSerializable(typeof(AlertDeleteResult))]
// Watchlist
[JsonSerializable(typeof(WatchlistResult))]
[JsonSerializable(typeof(WatchlistAddResult))]
[JsonSerializable(typeof(WatchlistAddBulkResult))]
[JsonSerializable(typeof(WatchlistRemoveResult))]
// Layout/Pane/Tab/Screenshot
[JsonSerializable(typeof(LayoutListResult))]
[JsonSerializable(typeof(LayoutSwitchResult))]
[JsonSerializable(typeof(PaneListResult))]
[JsonSerializable(typeof(PaneLayoutResult))]
[JsonSerializable(typeof(PaneFocusResult))]
[JsonSerializable(typeof(PaneSymbolResult))]
[JsonSerializable(typeof(TabListResult))]
[JsonSerializable(typeof(TabNewResult))]
[JsonSerializable(typeof(TabCloseResult))]
[JsonSerializable(typeof(TabSwitchResult))]
[JsonSerializable(typeof(ScreenshotResult))]
// Ui
[JsonSerializable(typeof(UiClickResult))]
[JsonSerializable(typeof(UiHoverResult))]
[JsonSerializable(typeof(UiKeyResult))]
[JsonSerializable(typeof(UiScrollResult))]
[JsonSerializable(typeof(UiFindResult))]
[JsonSerializable(typeof(UiEvalResult))]
[JsonSerializable(typeof(UiTypeResult))]
[JsonSerializable(typeof(UiPanelResult))]
[JsonSerializable(typeof(UiFullscreenResult))]
[JsonSerializable(typeof(UiMouseResult))]
public sealed partial class TvJsonContext : JsonSerializerContext
{
}
