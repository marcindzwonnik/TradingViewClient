namespace TradingView.Client;

/// <summary>Symbol, timeframe, chart type, visible range (<c>tv state|symbol|timeframe|type|info|search|range|scroll</c>).</summary>
public interface IChartApi
{
    /// <summary>Symbol, resolution, chart style and the studies on the chart.</summary>
    Task<ChartStateResult> GetStateAsync(CancellationToken cancellationToken = default);

    Task<SymbolResult> GetSymbolAsync(CancellationToken cancellationToken = default);

    /// <summary>Changes the chart symbol (use the exchange-qualified form, e.g. "NASDAQ:AAPL"). Waits up to 10 s for the chart to load.</summary>
    Task<SetSymbolResult> SetSymbolAsync(string symbol, CancellationToken cancellationToken = default);

    Task<TimeframeResult> GetTimeframeAsync(CancellationToken cancellationToken = default);

    Task<SetTimeframeResult> SetTimeframeAsync(Timeframe timeframe, CancellationToken cancellationToken = default);

    /// <summary>Reads the chart style. Styles outside <see cref="ChartType"/> are reported through <see cref="ChartTypeResult.TypeNum"/>.</summary>
    Task<ChartTypeResult> GetChartTypeAsync(CancellationToken cancellationToken = default);

    Task<ChartTypeResult> SetChartTypeAsync(ChartType chartType, CancellationToken cancellationToken = default);

    /// <summary>Metadata of the current symbol.</summary>
    Task<SymbolInfoResult> GetSymbolInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>Searches symbols (REST, at most 15 hits).</summary>
    Task<SymbolSearchResult> SearchSymbolsAsync(string query, CancellationToken cancellationToken = default);

    Task<VisibleRangeResult> GetVisibleRangeAsync(CancellationToken cancellationToken = default);

    Task<SetVisibleRangeResult> SetVisibleRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>Scrolls the chart so the given moment is centered (25 bars each side).</summary>
    Task<ScrollResult> ScrollToDateAsync(DateTimeOffset date, CancellationToken cancellationToken = default);
}
