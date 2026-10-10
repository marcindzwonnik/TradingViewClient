namespace TradingView.Client;

/// <summary>
/// Chart styles the CLI can set (<c>tv type NAME</c>). TradingView has more styles than these ten (for example
/// id 19 on some charts); reading them yields <see cref="ChartTypeResult.TypeNum"/> with a null <see cref="ChartTypeResult.Type"/>.
/// </summary>
public enum ChartType
{
    Bars = 0,
    Candles = 1,
    Line = 2,
    Area = 3,
    Renko = 4,
    Kagi = 5,
    PointAndFigure = 6,
    LineBreak = 7,
    HeikinAshi = 8,
    HollowCandles = 9,
}
