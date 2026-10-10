# TradingViewClient

Silnie typowany klient .NET 10 (`TradingView.Client`) dla [`tradingview-cli`](https://github.com/tradesdontlie/tradingview-cli) – CLI `tv` sterującego lokalnym TradingView Desktop przez CDP.

Status: etapy 1-2 z 5 gotowe (transport, błędy, JSON, Health, Chart, Data). Zobacz:

- [`docs/PLAN.md`](docs/PLAN.md) – zaakceptowany plan implementacji,
- [`docs/tv-cli-contract.md`](docs/tv-cli-contract.md) – katalog kształtów JSON każdej komendy `tv` (wejście do modeli C#).

## Szybki start

```csharp
using var tv = new TradingViewClient();
var status = await tv.Health.GetStatusAsync();
Console.WriteLine($"{status.ChartSymbol} @ {status.ChartResolution}");

var bars = await tv.Data.GetOhlcvAsync(50);
await tv.Chart.SetTimeframeAsync(Timeframe.Hours(4));
```

Testy offline: `dotnet test --project tests/TradingView.Client.Tests`.
Testy na żywo (wymagają TradingView z CDP; inaczej są pomijane): `dotnet test --project tests/TradingView.Client.IntegrationTests`.
Zrzut fixtur: `powershell -File tools/Capture-Fixtures.ps1` (domyślnie tylko komendy tylko-do-odczytu).
