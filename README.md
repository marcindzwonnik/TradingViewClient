# TradingViewClient

Silnie typowany klient .NET 10 (`TradingView.Client`) dla [`tradingview-cli`](https://github.com/tradesdontlie/tradingview-cli) – CLI `tv` sterującego lokalnym TradingView Desktop przez CDP.

Status: etapy 1-4 z 5 gotowe (transport, błędy, JSON, Health, Chart, Data, Pine, Indicators, Replay, Drawings, Alerts, Watchlist, Layouts, Panes, Tabs, Screenshot, Ui). Zobacz:

- [`docs/PLAN.md`](docs/PLAN.md) – zaakceptowany plan implementacji,
- [`docs/tv-cli-contract.md`](docs/tv-cli-contract.md) – katalog kształtów JSON każdej komendy `tv` (wejście do modeli C#).

## Szybki start

```csharp
using var tv = new TradingViewClient();
var status = await tv.Health.GetStatusAsync();
Console.WriteLine($"{status.ChartSymbol} @ {status.ChartResolution}");

var bars = await tv.Data.GetOhlcvAsync(50);
await tv.Chart.SetTimeframeAsync(Timeframe.Hours(4));

var rsi = await tv.Indicators.AddAsync("Relative Strength Index", new Dictionary<string, object?> { ["length"] = 7 });
var check = await tv.Pine.CheckAsync(File.ReadAllText("script.pine"));
await tv.Drawings.DrawHorizontalLineAsync(7900);
await tv.Alerts.CreateAsync(8000, AlertCondition.GreaterThan, "breakout");
var title = await tv.Ui.EvaluateAsync("document.title");
```

Testy offline: `dotnet test --project tests/TradingView.Client.Tests`.
Testy na żywo (wymagają TradingView z CDP; inaczej są pomijane): `dotnet test --project tests/TradingView.Client.IntegrationTests`.
Testy mutujące (zmieniają wykres, po sobie sprzątają): `TV_INTEGRATION_MUTATING=1`.
Zrzut fixtur: `powershell -File tools/Capture-Fixtures.ps1` (domyślnie tylko komendy tylko-do-odczytu; `-IncludeMutating` uruchamia scenariusze zmieniające wykres).
