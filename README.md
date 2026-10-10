# TradingView.Client

Silnie typowany klient .NET 10 dla [`tradingview-cli`](https://github.com/tradesdontlie/tradingview-cli) (komenda `tv`), które przez Chrome DevTools Protocol steruje lokalnie uruchomionym **TradingView Desktop**. Biblioteka uruchamia `tv` jako proces potomny i zamienia jego JSON w typowane rekordy, a strumienie (`tv stream *`) w `IAsyncEnumerable<T>`.

> Narzędzie nieoficjalne. Nie jest związane z TradingView Inc., działa wyłącznie na Twojej lokalnej instancji aplikacji. Korzystaj zgodnie z regulaminem TradingView.

## Wymagania

| Co | Wersja |
|---|---|
| .NET | 10 |
| Node.js | 20 lub nowszy |
| tradingview-cli | `1.0.0`, commit `3d2cbae` (kontrakt i fixtury zbadano na tej wersji) |
| TradingView Desktop | uruchomiony z CDP na porcie 9222 (`tv launch`) |

Instalacja CLI: `git clone https://github.com/tradesdontlie/tradingview-cli`, `npm install`, `npm link`. Sprawdź: `tv status` powinno zwrócić `"cdp_connected": true`.

## Instalacja biblioteki

```
dotnet add package TradingView.Client
dotnet add package TradingView.Client.DependencyInjection   # opcjonalnie
```

Pakiety nie są jeszcze opublikowane. Lokalnie: `dotnet pack -c Release -o artifacts` i dodaj `artifacts` jako źródło NuGet albo użyj `ProjectReference`. Przed publikacją trzeba wybrać licencję (`PackageLicenseExpression` w `src/Directory.Build.props`).

## Szybki start

```csharp
using TradingView.Client;

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

### Strumienie

```csharp
await foreach (var tick in tv.Streams.QuotesAsync(cancellationToken: ct))
    Console.WriteLine($"{tick.Symbol} {tick.Close}");

FeedSpec[] feeds = [new("CME_MINI:ES1!", Timeframe.Minutes(1)), FeedSpec.Parse("CME_MINI:ES1!@15")];
await foreach (var tick in tv.Streams.OhlcvAsync(feeds, TimeSpan.FromMilliseconds(250), ct))
    Console.WriteLine($"pane {tick.PaneIndex} {tick.Symbol}@{tick.Timeframe} {tick.Close}");
```

Anulowanie tokenu albo zwolnienie enumeratora zabija proces `node` (na Windows nie ma SIGINT, więc ubijane jest całe drzewo procesów). Gdy rynek jest zamknięty, strumień emituje jeden snapshot i milczy, bo CLI wypisuje tylko zmiany.

### Wstrzykiwanie zależności

```csharp
services.AddTradingViewClient(o => o.CdpPort = 9222);   // ITradingViewClient + każdy IChartApi, IDataApi, ...
services.Configure<TvClientOptions>(configuration.GetSection("TradingView"));   // też z konfiguracji
```

### Opcje (`TvClientOptions`)

| Opcja | Domyślnie | Znaczenie |
|---|---|---|
| `NodePath`, `CliScriptPath` | autodetekcja | jawna ścieżka do `node` i `tradingview-cli/src/cli/index.js` |
| `CdpHost`, `CdpPort` | CLI: `127.0.0.1:9222` | eksportowane jako `TV_CDP_HOST`/`TV_CDP_PORT` |
| `WorkingDirectory` | katalog repozytorium CLI | `tv status` sprawdza aktualizacje przez `git` w bieżącym katalogu |
| `DefaultTimeout` | 60 s | limit jednej komendy (po nim proces jest zabijany: `TvTimeoutException`) |
| `MaxConcurrency` | 1 | komendy działają na wspólnym wykresie, więc są szeregowane; strumienie nie liczą się |
| `ThrowOnOperationFailure` | `true` | exit 0 + `success:false` rzuca `TvOperationFailedException` (z typowanym `Result`) |
| `AutoOpenPineEditor` | `true` | komendy Pine ponawiają próbę po otwarciu edytora, gdy CLI go nie otworzy |

### Błędy

| Wyjątek | Kiedy |
|---|---|
| `TvConnectionException` | exit 2: brak połączenia CDP |
| `TvCommandException` | exit 1: zły argument, brak elementu, nieznana komenda |
| `TvOperationFailedException` | exit 0, ale `success:false` (np. `indicator add` nie utworzyło wskaźnika) |
| `TvTimeoutException` | komenda przekroczyła limit czasu |
| `TvResponseException` | odpowiedź to nie ten JSON (niesie surowy `StdOut`) |
| `TvCliNotFoundException` | nie znaleziono `tv` / `node` |

Wszystkie dziedziczą po `TvException` (`ExitCode`, `StdErr`). Wyniki częściowe (`success:true` + `error`, np. `alert list`) wracają jako obiekt z ustawionym `Error`. Pola, których model nie zna, trafiają do `AdditionalData`, więc nowsze wersje CLI niczego nie psują.

## Komenda → metoda

| `tv ...` | Metoda |
|---|---|
| `status` / `launch` / `discover` / `ui-state` / `update` | `Health.GetStatusAsync` / `LaunchAsync` / `DiscoverAsync` / `GetUiStateAsync` / `UpdateCliAsync` |
| `state` | `Chart.GetStateAsync` |
| `symbol [SYM]` | `Chart.GetSymbolAsync` / `SetSymbolAsync` |
| `timeframe [TF]` | `Chart.GetTimeframeAsync` / `SetTimeframeAsync(Timeframe)` |
| `type [NAME]` | `Chart.GetChartTypeAsync` / `SetChartTypeAsync(ChartType)` |
| `info` / `search Q` | `Chart.GetSymbolInfoAsync` / `SearchSymbolsAsync` |
| `range [--from --to]` / `scroll DATE` | `Chart.GetVisibleRangeAsync` / `SetVisibleRangeAsync` / `ScrollToDateAsync` |
| `quote [SYM]` | `Data.GetQuoteAsync` |
| `ohlcv [-n] [--summary]` | `Data.GetOhlcvAsync` / `GetOhlcvSummaryAsync` |
| `values` | `Data.GetStudyValuesAsync` |
| `data lines|labels|tables|boxes` | `Data.GetPineLinesAsync` / `GetPineLabelsAsync` / `GetPineTablesAsync` / `GetPineBoxesAsync` |
| `data strategy|trades|equity|depth` | `Data.GetStrategyResultsAsync` / `GetTradesAsync` / `GetEquityAsync` / `GetDepthAsync` |
| `data indicator ID` / `indicator get ID` | `Data.GetIndicatorAsync` / `Indicators.GetAsync` |
| `indicator add|remove|toggle|set` | `Indicators.AddAsync` / `RemoveAsync` / `SetVisibilityAsync` / `SetInputsAsync` |
| `pine get|set|compile|raw-compile` | `Pine.GetSourceAsync` / `SetSourceAsync` / `CompileAsync` / `RawCompileAsync` |
| `pine analyze|check` | `Pine.AnalyzeAsync` / `CheckAsync` |
| `pine save|new|open|list|errors|console` | `Pine.SaveAsync` / `NewAsync` / `OpenAsync` / `ListAsync` / `GetErrorsAsync` / `GetConsoleAsync` |
| `replay start|step|stop|status|autoplay|trade` | `Replay.StartAsync` / `StepAsync` / `StopAsync` / `GetStatusAsync` / `ToggleAutoplayAsync` / `TradeAsync` |
| `draw shape|list|get|remove|clear` | `Drawings.DrawAsync` (+ `DrawHorizontalLineAsync` itd.) / `ListAsync` / `GetAsync` / `RemoveAsync` / `ClearAsync` |
| `alert list|create|delete` | `Alerts.ListAsync` / `CreateAsync` / `DeleteAsync` / `DeleteAllAsync` |
| `watchlist get|add|add-bulk|remove` | `Watchlist.GetAsync` / `AddAsync` / `AddRangeAsync` / `RemoveAsync` |
| `layout list|switch` | `Layouts.ListAsync` / `SwitchAsync` |
| `pane list|layout|focus|symbol` | `Panes.ListAsync` / `SetLayoutAsync(PaneLayout)` / `FocusAsync` / `SetSymbolAsync` |
| `tab list|new|close|switch` | `Tabs.ListAsync` / `NewAsync` / `CloseAsync` / `SwitchAsync` |
| `screenshot [-r] [-o]` | `Screenshots.CaptureAsync` |
| `ui click|keyboard|hover|scroll|find` | `Ui.ClickAsync` / `PressKeyAsync` / `HoverAsync` / `ScrollAsync` / `FindAsync` |
| `ui eval|type|panel|fullscreen|mouse` | `Ui.EvaluateAsync` / `TypeTextAsync` / `PanelAsync` / `ToggleFullscreenAsync` / `MouseClickAsync` |
| `stream quote|bars|values` | `Streams.QuotesAsync` / `BarsAsync` / `StudyValuesAsync` |
| `stream lines|labels|tables|all` | `Streams.PineLinesAsync` / `PineLabelsAsync` / `PineTablesAsync` / `AllPanesAsync` |
| `stream ohlcv SYM@TF...` | `Streams.OhlcvAsync(IEnumerable<FeedSpec>)` |

## Na co uważać

Pełna lista, z dowodami z żywego TradingView, jest na końcu [`docs/tv-cli-contract.md`](docs/tv-cli-contract.md). Najważniejsze:

- **Windows:** `tv` to shim `tv.cmd`. Klient uruchamia `node index.js` bezpośrednio z listą argumentów, więc `!`, `^`, `&` i cudzysłowy (symbole, JSON, Pine) przechodzą bez zmian.
- **Crash Node po poprawnym JSON-ie:** niektóre komendy (`search`, `pine check`, `tab new`, `tab close`) kończą się na Windows kodem `0xC0000409` po wypisaniu kompletnego wyniku. Klient traktuje to jako sukces, gdy stdout jest poprawnym JSON-em.
- **`tab close` / `tab switch`:** upstream nie aktywuje z powrotem karty z wykresem, a `screenshot` karty w tle wisi.
- **`pine compile`** nowego skryptu otwiera w TradingView modal „Save script”.
- **Nazwy wskaźników** to pełne nazwy TradingView (`Relative Strength Index`, nie `RSI`).
- **`alert delete --all`** (`DeleteAllAsync`) kasuje wszystkie alerty konta.
- **Współrzędne `ui mouse`** są w pikselach CSS, nie w pikselach zrzutu ekranu.

## Testy

```
dotnet test --project tests/TradingView.Client.Tests                 # offline: FakeTransport + fixtury, test dryfu kontraktu
dotnet test --project tests/TradingView.Client.IntegrationTests      # na żywo, tylko odczyt; bez CDP testy są pomijane
TV_INTEGRATION_MUTATING=1 dotnet test --project tests/TradingView.Client.IntegrationTests   # + zmiany wykresu, każdy test po sobie sprząta
```

(`dotnet test` na .NET 10 wymaga `--project`; runner to Microsoft.Testing.Platform, ustawiony w `global.json`.)

**Test dryfu kontraktu**: każda fixtura (`tests/TradingView.Client.Tests/Fixtures`) po deserializacji musi mieć puste `AdditionalData`. Jeśli CLI doda pole, którego model nie zna, test to wykryje.

**Fixtury** zrzuca `tools/Capture-Fixtures.ps1` (Windows PowerShell 5.1 lub PowerShell 7). Domyślnie tylko komendy tylko-do-odczytu; `-IncludeMutating` uruchamia scenariusze zmieniające wykres (z przywracaniem stanu), `-Area Pine,Ui` ogranicza obszary. Skrypt maskuje dane konta (nazwy alertów, layoutów i skryptów, identyfikatory kart, zaszyfrowane źródła Pine). Nie zrzuca `launch`, `update` ani `pine save`.

## Układ repozytorium

```
src/TradingView.Client/                      biblioteka (AOT-compatible): Transport, Json, Primitives, Models, Api
src/TradingView.Client.DependencyInjection/  AddTradingViewClient
tests/TradingView.Client.Tests/              xUnit v3, offline
tests/TradingView.Client.IntegrationTests/   xUnit v3, na żywo
samples/TradingView.Client.Sample/           konsola: status | quote | ohlcv | stream
tools/Capture-Fixtures.ps1                   zrzut prawdziwych odpowiedzi `tv`
docs/                                        PLAN.md, tv-cli-contract.md (kontrakt JSON + korekty z żywego TradingView)
```

Sample: `dotnet run --project samples/TradingView.Client.Sample -- stream "CME_MINI:ES1!@1" "CME_MINI:ES1!@15" --for 20`.
