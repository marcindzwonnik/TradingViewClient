# Plan: TradingView.Client – silnie typowany klient .NET 10 dla `tradingview-cli`

## Context

`tradingview-cli` (https://github.com/tradesdontlie/tradingview-cli, zbadany commit `3d2cbae`) to Node.js CLI `tv`, które przez Chrome DevTools Protocol (port 9222) steruje lokalnym TradingView Desktop. Każda komenda wypisuje JSON (sformatowany, wieloliniowy) na stdout; błędy jako `{success:false,error}` na stderr; exit `0` OK / `1` błąd / `2` brak połączenia CDP; `stream *` emituje JSON Lines bez końca.

Cel: biblioteka C#/.NET 10, która daje silnie typowane API nad tym CLI (**opcja A – wrapper procesu `tv`**), tak by kod .NET mógł czytać wykresy, dane, Pine, replay, alerty itd. oraz konsumować strumienie jako `IAsyncEnumerable<T>`. Niestabilna warstwa (wstrzykiwany JS do TradingView) zostaje w upstreamowym CLI; my utrzymujemy kontrakt i typy.

Ustalenia z użytkownikiem: opcja A; nazwa/namespace `TradingView.Client`; **realizacja w lokalnej sesji Claude Code na Windows** (tam działa TradingView i `tv` zainstalowane przez `npm link`).

## Krok 0 – przekazanie do sesji lokalnej (w tej sesji chmurowej, po akceptacji)

1. Zapisać w repo `docs/PLAN.md` (ten plan) i `docs/tv-cli-contract.md` – pełny katalog kształtów JSON każdej komendy/strumienia (zebrany z `src/core/*.js` upstreamu, z odnośnikami file:line, jednostkami czasu, enumami, przypadkami „exit 0 + success:false”).
2. Commit + `git push -u origin claude/serene-shannon-cgi7dv`.
3. Użytkownik lokalnie: `git fetch && git checkout claude/serene-shannon-cgi7dv`, uruchamia Claude Code i zleca wykonanie `docs/PLAN.md`.

Wymagania lokalne (sprawdzane na starcie etapu 1): `dotnet --list-sdks` zawiera 10.x, `node -v` ≥ 20, `where tv`, `tv status` → `cdp_connected: true`.

## Struktura repozytorium

```
TradingView.Client.slnx
Directory.Build.props        net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, LangVersion latest
Directory.Packages.props     central package management
src/TradingView.Client/                       (IsAotCompatible, zależność tylko M.E.Logging.Abstractions)
  Transport/   ITvTransport, TvCommand, TvRawResult, CliProcessTransport, TvCliLocator
  Json/        TvJsonContext (source-gen), UnixSecondsConverter, UnixMillisecondsConverter, TolerantEnumConverter<T>
  Primitives/  Timeframe, FeedSpec, enumy (ChartType, ShapeType, AlertCondition, PaneLayout, UiSelector,
               FindStrategy, ScreenshotRegion, PineScriptType, ReplayTradeAction, ReplaySpeed, PanelName,
               PanelAction, ScrollDirection, KeyModifiers [Flags])
  Models/      rekordy per obszar (Health/, Chart/, Data/, Pine/, Replay/, Drawing/, Alerts/, Watchlist/,
               Layout/, Indicators/, Ui/, Panes/, Tabs/, Streams/)
  Api/         IChartApi+ChartApi, IDataApi, IPineApi, IReplayApi, IDrawingApi, IAlertApi, IWatchlistApi,
               ILayoutApi, IIndicatorApi, IUiApi, IPaneApi, ITabApi, IScreenshotApi, IHealthApi, IStreamApi
  Errors/      TvException → TvConnectionException (exit 2), TvCommandException (exit 1),
               TvOperationFailedException (exit 0 + success:false; niesie typowany Result)
  TradingViewClient.cs / ITradingViewClient   fasada: tv.Health, tv.Chart, tv.Data, tv.Pine, … tv.Streams
  TvClientOptions.cs
src/TradingView.Client.DependencyInjection/   AddTradingViewClient(Action<TvClientOptions>)
tests/TradingView.Client.Tests/               xUnit v3, offline, FakeTransport + Fixtures/*.json|*.jsonl
tests/TradingView.Client.IntegrationTests/    xUnit v3, live; Assert.Skip gdy CDP niedostępne
samples/TradingView.Client.Sample/            konsola: status, quote, ohlcv, stream ohlcv
tools/Capture-Fixtures.ps1                    zrzut prawdziwych odpowiedzi `tv` do Fixtures/
```

## Transport (kluczowe dla Windows)

- `ITvTransport`:
  `Task<TvRawResult> ExecuteAsync(TvCommand, CancellationToken)` i `IAsyncEnumerable<string> StreamLinesAsync(TvCommand, CancellationToken)`;
  `TvCommand(IReadOnlyList<string> Args, string? StdIn = null, TimeSpan? Timeout = null)`.
- `TvCliLocator` – na Windows `tv` to shim `tv.cmd`, którego `Process.Start` bez powłoki nie uruchomi, a `cmd.exe /c` psuje argumenty (`!` w `CME_MINI:ES1!`, `^`, `&`, cudzysłowy w JSON i Pine). Dlatego uruchamiamy **`node.exe <index.js>` bezpośrednio z `ProcessStartInfo.ArgumentList`**. Kolejność rozwiązywania:
  1. `TvClientOptions.NodePath` + `CliScriptPath` (jawnie),
  2. znalezienie `tv.cmd` w `PATH` i wyciągnięcie ścieżki do `...\src\cli\index.js` ze shima npm (względem `%dp0%`, podążając za linkiem `npm link`),
  3. `npm root -g` + `tradingview-cli\src\cli\index.js`,
  4. poza Windows: `tv` z PATH.
  Wynik cache’owany; czytelny wyjątek z instrukcją, gdy nic nie znaleziono.
- `CliProcessTransport`: UTF-8 dla stdin/stdout/stderr; zmienne `TV_CDP_HOST`/`TV_CDP_PORT` z opcji; stdin zawsze przekierowane i zamykane (Pine source przez stdin, bez plików tymczasowych); timeout (domyślnie 60 s) i anulowanie → `Kill(entireProcessTree: true)`; stdout i stderr czytane równolegle (brak deadlocka).
- Współbieżność: `SemaphoreSlim(MaxConcurrency = 1)` – komendy działają na wspólnym aktywnym wykresie. Strumienie nie zajmują semafora.
- Streamy: `ReadLineAsync(ct)` po stdout, każda niepusta linia → deserializacja; stderr → `ILogger` (Debug/Warning); anulowanie zabija drzewo procesu (na Windows nie ma SIGINT); niezerowy exit w trakcie → `TvException` z treścią stderr.

## Mapowanie odpowiedzi

- Exit 0 → deserializacja całego stdout (`TvJsonContext`). Exit 1 → `TvCommandException(message)` (stderr jako JSON albo surowy tekst – np. „Unknown command”). Exit 2 → `TvConnectionException`.
- Bazowy `abstract record TvResult { bool Success; string? Error; string? Note; [JsonExtensionData] Dictionary<string, JsonElement>? AdditionalData; }` – nowe pola z CLI nie łamią deserializacji ani nie giną.
- Exit 0 + `success:false` (indicator add, data strategy/trades/equity, alert create/delete, watchlist add/add-bulk/remove, update, tab close): `TvClientOptions.ThrowOnOperationFailure = true` (domyślnie) → `TvOperationFailedException` z `Result`. `success:true` + `error` (alert list, pine list, layout list) to wynik częściowy – zwracamy obiekt z ustawionym `Error`, bez wyjątku. `pine compile` z `has_errors` / `pine check` z `compiled:false` – zwykły wynik (`HasErrors`, `Errors`).
- JSON: snake_case naming policy + jawne `[JsonPropertyName]` dla wyjątków (`chartType` w `state`/`ui-state`, `methodCount`); czasy barów → `DateTimeOffset` z unix **sekund**, `_ts`/`observed_at` → z unix **milisekund**; ceny `double` (źródłem są liczby JS); pola `any` → `JsonElement`; enumy przez `TolerantEnumConverter` (nieznana wartość → `Unknown`, zamiast wyjątku); `change_pct` ("1.23%") jako string + wyliczana właściwość `double? ChangePercentValue`; wartości watchlisty i `values` (data window) to stringi – zostają stringami (stream `values` daje liczby – osobny model).
- Rozbieżności kontraktu do uwzględnienia (z katalogu): `data tables` → `rows: string[]` vs stream tables → `string[][]`; stream `ohlcv` nie ma `_ts/_stream`; `ohlcv -n` max 500; `data trades -n` max 20; `draw shape` wymaga `--time` nawet dla horizontal_line; dozwolone `ReplaySpeed` = 100,143,200,300,1000,2000,3000,5000,10000; minimalny interwał multi-feed = 100 ms.

## Prymitywy wejściowe (walidacja przed uruchomieniem procesu)

- `Timeframe` (readonly record struct): `Seconds(n)`→"nS", `Minutes(n)`→"n", `Hours(n)`→"60·n", `Days(n)`→"nD", `Weeks`, `Months`; `Parse`, implicit z `string`, `ToString()` = forma kanoniczna TradingView.
- `FeedSpec(string Symbol, Timeframe Timeframe)` → `"SYM@TF"`; walidacja niepustych części i duplikatów jak w `multi-feed.js:parseFeedSpecs`.
- Enumy mapowane na dokładne wartości CLI (np. `PaneLayout.Grid2x2` → `"4"`, `AlertCondition.GreaterThan` → `"greater_than"`, `ChartType` → nazwa). Dla `ShapeType` dodatkowo przeciążenie z surowym `string` (CLI przepuszcza dowolną nazwę kształtu TradingView).
- Wejścia wskaźników: `IReadOnlyDictionary<string, object?>` / `JsonObject` → serializowane do argumentu `-i`.

## Powierzchnia API (1 metoda ≈ 1 komenda CLI; wszystkie `Async` z `CancellationToken`)

- **Health**: `GetStatusAsync`, `LaunchAsync(port?, killExisting)`, `DiscoverAsync`, `GetUiStateAsync`, `UpdateCliAsync`.
- **Chart**: `GetStateAsync`, `GetSymbolAsync`/`SetSymbolAsync`, `GetTimeframeAsync`/`SetTimeframeAsync`, `GetChartTypeAsync`/`SetChartTypeAsync`, `GetSymbolInfoAsync`, `SearchSymbolsAsync`, `GetVisibleRangeAsync`/`SetVisibleRangeAsync`, `ScrollToDateAsync`.
- **Data**: `GetQuoteAsync(symbol?)`, `GetOhlcvAsync(count)`, `GetOhlcvSummaryAsync(count)`, `GetStudyValuesAsync`, `GetPineLinesAsync`/`LabelsAsync`/`TablesAsync`/`BoxesAsync(filter, verbose)`, `GetStrategyResultsAsync`, `GetTradesAsync(max)`, `GetEquityAsync`, `GetDepthAsync`, `GetIndicatorAsync(id)`.
- **Pine**: `GetSourceAsync`, `SetSourceAsync`, `CompileAsync`, `RawCompileAsync`, `AnalyzeAsync(source)`, `CheckAsync(source)`, `SaveAsync`, `NewAsync(PineScriptType)`, `OpenAsync(name)`, `ListAsync`, `GetErrorsAsync`, `GetConsoleAsync`.
- **Screenshot**: `CaptureAsync(region, fileName?)` → `FilePath`, `SizeBytes`.
- **Replay**: `StartAsync(DateOnly?)`, `StepAsync`, `StopAsync`, `GetStatusAsync`, `ToggleAutoplayAsync(ReplaySpeed?)`, `TradeAsync(ReplayTradeAction)`.
- **Drawings**: `DrawAsync(ShapeRequest)` + helpery `DrawHorizontalLineAsync`, `DrawTrendLineAsync`, `DrawRectangleAsync`, `DrawTextAsync`; `ListAsync`, `GetAsync`, `RemoveAsync`, `ClearAsync`.
- **Alerts**: `ListAsync`, `CreateAsync(price, condition, message?)`, `DeleteAsync(id)`, `DeleteAllAsync`.
- **Watchlist**: `GetAsync`, `AddAsync`, `AddRangeAsync`, `RemoveAsync(params string[])`.
- **Layouts**: `ListAsync`, `SwitchAsync(nameOrId)`.
- **Indicators**: `AddAsync(name, inputs?)`, `RemoveAsync`, `SetVisibilityAsync`, `SetInputsAsync`, `GetAsync`.
- **Ui**: `ClickAsync`, `PressKeyAsync(key, KeyModifiers)`, `HoverAsync`, `ScrollAsync`, `FindAsync`, `EvaluateAsync` (→ `JsonElement`) + `EvaluateAsync<T>(JsonTypeInfo<T>)`, `TypeTextAsync`, `PanelAsync`, `ToggleFullscreenAsync`, `MouseClickAsync`.
- **Panes**: `ListAsync`, `SetLayoutAsync(PaneLayout)`, `FocusAsync`, `SetSymbolAsync`.
- **Tabs**: `ListAsync`, `NewAsync`, `CloseAsync`, `SwitchAsync`.
- **Streams** (`IAsyncEnumerable<T>`): `QuotesAsync`, `BarsAsync`, `StudyValuesAsync`, `PineLinesAsync`/`PineLabelsAsync`/`PineTablesAsync(filter)`, `AllPanesAsync`, `OhlcvAsync(IEnumerable<FeedSpec>, interval)`.

## Testy

1. **Fixtury z żywego TradingView** – `tools/Capture-Fixtures.ps1` uruchamia komendy `tv` i zapisuje stdout do `tests/TradingView.Client.Tests/Fixtures/<obszar>/<komenda>.json` (streamy: ok. 5 s → `.jsonl`). Domyślnie tylko komendy read-only. Przełącznik `-IncludeMutating` uruchamia scenariusz z porządkowaniem: zapis symbolu/TF → horizontal line → get/remove; alert create → delete; indicator add „Volume” → get/set/toggle/remove; replay start/step/status/stop; pine analyze/check przykładowego skryptu; przywrócenie symbolu/TF. **Przed trybem mutującym pytamy użytkownika o zgodę** (to jego wykres i konto).
2. **Unit (offline, `FakeTransport`)**:
   - tabelaryczne testy budowania argumentów dla każdej metody;
   - deserializacja każdej fixtury;
   - **test dryfu kontraktu**: każda fixtura po deserializacji ma puste `AdditionalData` (wykrywa pola brakujące w modelach);
   - mapowanie błędów (exit 1/2, tekstowy stderr, soft failure);
   - parsowanie JSONL i anulowanie strumieni;
   - `TvCliLocator` na przykładowym shimie `tv.cmd`;
   - walidacja `Timeframe` i `FeedSpec`.
3. **Integracyjne (live)**: `Assert.Skip`, gdy `http://127.0.0.1:9222/json/version` nie odpowiada. Testy read-only są domyślne. Mutujące wymagają `TV_INTEGRATION_MUTATING=1`, każdy sprząta po sobie (`IAsyncLifetime` przywraca symbol/TF i usuwa utworzone obiekty).

## Etapy (każdy = osobny commit, push na `claude/serene-shannon-cgi7dv`)

1. Weryfikacja wymagań lokalnych; szkielet solution (props, projekty, CPM); Transport + `TvCliLocator` + błędy + `TvJsonContext` + konwertery; `Health`; `Capture-Fixtures.ps1` i pierwszy zrzut read-only; testy unit dla tej warstwy.
2. Chart + Data (modele z fixtur, testy unit + integracyjne read-only).
3. Pine, Indicators, Replay (+ mutujący zrzut fixtur za zgodą).
4. Drawings, Alerts, Watchlist, Layouts, Panes, Tabs, Screenshot, Ui.
5. Streams (`IAsyncEnumerable`), pakiet DI, sample, README (instalacja, przykłady, tabela komenda→metoda, wymagana wersja CLI), metadane NuGet (`dotnet pack`).

## Weryfikacja end-to-end

- `dotnet build -warnaserror` całego solution.
- `dotnet test tests/TradingView.Client.Tests` – zielone, w tym test dryfu kontraktu.
- `pwsh tools/Capture-Fixtures.ps1` przy uruchomionym TradingView (`tv status` → `cdp_connected: true`).
- `dotnet test tests/TradingView.Client.IntegrationTests`, potem z `TV_INTEGRATION_MUTATING=1` (za zgodą).
- `dotnet run --project samples/TradingView.Client.Sample -- quote` i `-- stream CME_MINI:ES1!@1 NASDAQ:AAPL@15`: 10 s strumienia, potem Ctrl+C. Sprawdzić, że proces `node` zostaje zabity (brak osieroconych procesów w Menedżerze zadań).
- `dotnet pack` → `.nupkg` z XML docs.
