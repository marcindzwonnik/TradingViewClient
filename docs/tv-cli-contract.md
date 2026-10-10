# Kontrakt JSON `tradingview-cli` (wejście do modeli C#)

Źródło: https://github.com/tradesdontlie/tradingview-cli @ `3d2cbae` („fix: scale streams across plan-limited layouts”).
Ścieżki `plik:linia` są względne do katalogu głównego tamtego repozytorium (`src/core/*.js`, `src/cli/*`).
Ten katalog powstał z lektury kodu, a nie z prawdziwych odpowiedzi. Przed napisaniem modeli trzeba go potwierdzić fixturami z żywego TradingView (`tools/Capture-Fixtures.ps1`).

## 0. Zasady ogólne (dotyczą wszystkich komend)

- **Sukces.** `src/cli/router.js:133-135` robi `console.log(JSON.stringify(result, null, 2))`, czyli wypisuje JSON **sformatowany na wiele linii**, i kończy z exit 0. Router **nie sprawdza** pola `success`. Wynik z `success:false` albo z polem `error` też kończy się kodem **0**.
- **Wyjątek.** `router.js:141-150` wypisuje `{ "success": false, "error": string }` na **stderr**. Exit **2**, gdy komunikat pasuje do `/CDP|connection|ECONNREFUSED|not running/i`, w przeciwnym razie exit **1**.
- **Pola `undefined`.** `JSON.stringify` je pomija, więc każde pole typu „`T | undefined`” jest opcjonalne (nullable w C#).
- **Wartości liczbowe.** Wartości wracają z `Runtime.evaluate` z `returnByValue: true` (`src/connection.js:134-146`), więc NaN i Infinity w obiektach stają się `null`.
- **Komunikaty tekstowe.** Nieznana komenda lub subkomenda daje **zwykły tekst** na stderr i exit 1. `--help` daje zwykły tekst i exit 0.
- **Czas.** Każdy `time`/`bar_time` z barów TradingView (`bars.valueAt(i)[0]`) to unix **sekundy**. Każdy `_ts` i `observed_at` to `Date.now()`, czyli unix **milisekundy**.

Typy wspólne:
```ts
type Bar = { time: number /*unix s*/, open: number, high: number, low: number, close: number, volume: number /*0 gdy brak*/ }
type PineMarker = { line: number, column: number, message: string, severity: number /* Monaco: 1 hint, 2 info, 4 warning, 8 error */ }
```

---

## 1. Health: status / launch / update / discover / ui-state

**status** → `healthCheck`, `src/core/health.js:71-82`
```ts
{ success: true, cdp_connected: true, target_id: string, target_url: string, target_title: string,
  chart_symbol: string /*'unknown' przy błędzie*/, chart_resolution: string /*'unknown'*/,
  chart_type: number | null, api_available: boolean,
  update?: { update_available: boolean, local_commit: string /*8 znaków*/, latest_commit: string, hint?: string } } // health.js:32-37, tylko gdy sprawdzenie GitHuba się udało
```

**launch** (`--port`, `--no-kill`) → `health.js:388-393`, gdy CDP wstał, albo `health.js:396-400`, gdy nie wstał. Rzuca, gdy nie znajdzie binarki (`:349`).
```ts
// CDP gotowe
{ success: true, platform: string, binary: string, pid: number, cdp_port: number, cdp_url: string,
  browser: string, user_agent: string, msix_local_copy?: true }
// CDP niegotowe (nadal exit 0)
{ success: true, platform, binary, pid, cdp_port: number, cdp_ready: false, msix_local_copy?: true, warning: string }
```

**update** → `src/core/update.js`. Kilka gałęzi zwraca `success:false` z exit 0:
```ts
| { success: false, error: string }                                  // :29-32, :39, :61, :70-73
| { success: false, branch: string, error: string }                  // :42-45 (nie na main)
| { success: false, error: string, changed_files: string[] /*≤10*/ } // :50-54 (brudne drzewo)
| { success: true, updated: false, status: 'up_to_date', commit: string } // :65
| { success: true, updated: true, from_commit: string, to_commit: string, commits_pulled: number,
    deps_installed: boolean, warning?: string, restart_required: true, note: string } // :93-103
```

**discover** → `health.js:128`
```ts
type ApiProbe = { available: boolean, path?: string, methodCount?: number /*camelCase!*/, methods?: string[], error?: string }
{ success: true, apis_available: number, apis_total: number /*6*/,
  apis: { chartApi: ApiProbe, chartWidgetCollection: ApiProbe, chartApiInstance: ApiProbe,
          bottomWidgetBar: ApiProbe, replayApi: ApiProbe, alertService: ApiProbe } }
```

**ui-state** → `health.js:199` (budowane w `:134-195`)
```ts
{ success: true,
  bottom_panel: { open: boolean, height: number }, right_panel: { open: boolean, width: number },
  pine_editor: { open: boolean, width: number, height: number }, strategy_tester: { open: boolean }, widgetbar: { open: boolean },
  buttons: Partial<Record<'top_bar'|'toolbar'|'left_sidebar'|'pine_header'|'bottom_bar'|'other',
           { label: string, disabled: boolean, x: number, y: number }[]>>,
  key_buttons: Partial<Record<'add_to_chart'|'save_and_add'|'update_on_chart'|'save'|'saved'|'publish_script'|'compile_errors'|'unsaved_version',
           { text: string, disabled: boolean, visible: boolean }>>,
  chart: { symbol: string, resolution: string, chartType: number /*camelCase!*/, study_count: number } | { error: string },
  replay: { available: any, started: any } | { error: string } }
```

## 2. Chart: state / symbol / timeframe / type / info / search / range / scroll

- **state** → `src/core/chart.js:37`
  `{ success: true, symbol: string, resolution: string, chartType: number /*camelCase!*/, studies: { id: string, name: string }[] }`
- **symbol**
  - odczyt (`src/cli/commands/chart.js:16`): `{ success: true, symbol: string, resolution: string }`
  - zmiana (`chart.js:52`): `{ success: true, symbol: string, chart_ready: boolean }`. `chart_ready` jest `false` po przekroczeniu 10 s (`src/wait.js:71`).
- **timeframe**
  - odczyt (`commands/chart.js:26`): `{ success: true, resolution: string, symbol: string }`
  - zmiana (`chart.js:64`): `{ success: true, timeframe: string, chart_ready: boolean }`
- **type**
  - odczyt (`commands/chart.js:37`): `{ success: true, chart_type: string /*nazwa albo liczba spoza tabeli*/, type_num: number }`
  - zmiana (`chart.js:84`): `{ success: true, chart_type: string /*echo wejścia*/, type_num: number }`. Rzuca przy złej wartości (`:76`).
  - Enum (`chart.js:69-73`): `Bars=0, Candles=1, Line=2, Area=3, Renko=4, Kagi=5, PointAndFigure=6, LineBreak=7, HeikinAshi=8, HollowCandles=9`. Nazwy są case-sensitive; liczby 0-9 też są akceptowane.
- **info** → `chart.js:270`
  `{ success: true, symbol?: string, full_name?: string, exchange?: string, description?: string, type?: string, pro_name?: string, typespecs?: string[], resolution: string, chart_type: number }`
- **search** → `chart.js:299`
  `{ success: true, query: string, source: 'rest_api', count: number, results: { symbol, description, exchange, type, full_name: string }[] /*≤15*/ }`
- **range**
  - odczyt (`chart.js:165`): `{ success: true, visible_range: { from: number, to: number } /*unix s*/, bars_range: { from: number, to: number } /*indeksy barów, mogą być ułamkowe*/ }`
  - zmiana, wymaga `--from` i `--to` (`chart.js:214`): `{ success: true, requested: { from, to }, actual: { from, to, error?: string } }`, wartości w unix s.
- **scroll** → `chart.js:254`
  `{ success: true, date: string /*wejście*/, centered_on: number /*unix s*/, resolution: string, window: { from: number, to: number } /*unix s, ±25 barów*/ }`
  Wejście: ISO albo unix s. Rzuca przy nieparsowalnej dacie (`:222`).

## 3. Data: quote / ohlcv / values / data *

- **quote [SYMBOL]** → `src/core/data.js:433`. Rzuca, gdy nie ma ani `last`, ani `close` (`:432`). Jeśli podasz symbol, wykres jest na chwilę przełączany na ten symbol, a potem przywracany.
  ```ts
  { success: true, symbol: string, time?: number /*unix s*/, open?, high?, low?, close?, last?: number /*=close*/, volume?: number,
    bid?: number|null, ask?: number|null /*ze scrapingu DOM*/, header_price?: number, description?: string, exchange?: string, type?: string }
  ```
- **ohlcv** (`-n`, domyślnie 100, maksymalnie 500; `0` też daje 100) → `data.js:181`. Rzuca, gdy nie ma barów (`:158`).
  `{ success: true, bar_count: number, total_available: number, source: 'direct_bars', bars: Bar[] }`
- **ohlcv --summary** → `data.js:168-178`
  ```ts
  { success: true, bar_count: number, period: { from: number, to: number } /*unix s*/, open, close, high, low, range, change: number,
    change_pct: string /*np. "1.23%" – STRING*/, avg_volume: number /*integer*/, last_5_bars: Bar[] }
  ```
- **values** → `data.js:535`. Wartości to sformatowane **stringi** z okna danych.
  `{ success: true, study_count: number, studies: { id: string|null, name: string, inputs: Record<string,any>|null, values: Record<string,string> }[] }`
- **data lines** (`-f`, `-v`) → `data.js:541` (pusty wynik), `:559`
  `{ success: true, study_count, studies: { name: string, total_lines: number, horizontal_levels: number[] /*malejąco*/, all_lines?: { id, y1: number|null, y2: number|null, x1: any, x2: any, horizontal: boolean, style: any, width: any, color: any }[] /*tylko z -v*/ }[] }`
- **data labels** (`-f`, `-n` domyślnie 50, `-v`) → `data.js:565`, `:579`
  `{ success: true, study_count, studies: { name: string, total_labels: number, showing: number, labels: Label[] /*ostatnie N*/ }[] }`
  - `Label = { text: string, price: number|null }`
  - z `-v`: `{ id, text, price, x: any, yloc: any, size: any, textColor: any, color: any }`
- **data tables** → `data.js:585`, `:607`
  `{ success: true, study_count, studies: { name: string, tables: { rows: string[] /*komórki sklejone " | "*/ }[] }[] }`
  Ten kształt **różni się od streamu** (patrz sekcja 15).
- **data boxes** (`-f`, `-v`) → `data.js:613`, `:631`
  `{ success: true, study_count, studies: { name: string, total_boxes: number, zones: { high: number, low: number }[], all_boxes?: { id, high: number|null, low: number|null, x1: any, x2: any, borderColor: any, bgColor: any }[] }[] }`
- **data strategy** → `data.js:285-292`. `success = metric_count > 0`, więc możliwe jest **false przy exit 0** z polem `error` (`:251`, `:253`, `:282`).
  ```ts
  { success: boolean, metric_count: number, strategy?: string|null, currency?: string|null, source?: 'internal_api',
    metrics: { net_profit?, net_profit_percent?, gross_profit?, gross_loss?, profit_factor?, max_drawdown?, max_drawdown_percent?,
               total_trades?, winning_trades?, losing_trades?, percent_profitable?, avg_trade?, largest_win?, largest_loss?,
               commission_paid?, sharpe_ratio?, sortino_ratio?, buy_hold_return?, open_pl?: number },
    unhidden_strategies?: string[], note?: string, error?: string }
  ```
- **data trades** (`-n` domyślnie 20, **maksymalnie 20**) → `data.js:330-336`. `success = trade_count > 0`.
  ```ts
  { success: boolean, trade_count: number, total_orders: number, source?: 'internal_api',
    trades: { id: any, type: any, side: 'buy'|'sell', entry: any, price: number, qty: number, time_index: any }[],
    unhidden_strategies?: string[], note?: string, error?: string }
  ```
- **data equity** → `data.js:361-367`. `success = data_points > 0` i **zwykle wynosi false**, bo krzywa kapitału rzadko jest dostępna (zamiast niej zwracane jest `note`).
  `{ success: boolean, data_points: number, source?: 'internal_api', data: any[], buy_hold_points?: number, note?: string, unhidden_strategies?: string[], error?: string }`
- **data depth** → `data.js:493`. Rzuca, gdy brak panelu DOM (`:492`).
  `{ success: true, bid_levels: number, ask_levels: number, spread?: number|null, bids: {price:number,size:number}[], asks: {price,size}[], raw_values?: number[], note?: string }`
- **data indicator ID** / **indicator get ID** → `data.js:207`. Rzuca „Study not found” (`:197`).
  `{ success: true, entity_id: string, visible: boolean|null, inputs: { id: string, value: any }[] | null }`

## 4. Pine (`src/core/pine.js`)

- **get** → `:263`: `{ success: true, source: string, line_count: number, char_count: number }`
- **set** (źródło przez stdin albo `--file`) → `:281`: `{ success: true, lines_set: number }`
- **compile** → `:499-505`. **`success:true` nawet przy błędach kompilacji.**
  `{ success: true, button_clicked: 'Save and add to chart'|'Add to chart'|'Update on chart'|'Pine Save'|'keyboard_shortcut', has_errors: boolean, errors: PineMarker[], study_added: boolean|null }`
- **raw-compile** → `:319`: `{ success: true, button_clicked: string, source: 'dom_fallback' }`
- **analyze** (offline, przez stdin) → `:178-183`
  `{ success: true, issue_count: number, diagnostics: { line, column: number, message: string, severity: 'error'|'warning'|'info' }[], note?: string }`
- **check** (kompilacja po stronie serwera, przez stdin) → `:234-242`. Zwraca `success:true` także przy `compiled:false`.
  `{ success: true, compiled: boolean, error_count: number, warning_count: number, errors?: { line?, column?, end_line?, end_column?: number, message: string }[], warnings?: { line?, column?: number, message: string }[], note?: string }`
- **save** → `:376`: `{ success: true, action: 'saved_with_dialog' | 'Ctrl+S_dispatched' }`
- **new [type]** → `:534`: `{ success: true, type: string, action: 'new_script_created', template?: 'indicator'|'strategy'|'library' }`
  Dozwolone typy: `indicator` (domyślny) | `strategy` | `library`.
- **open NAME** → `:588`: `{ success: true, name: string, script_id: string, lines: number, source: 'internal_api', opened: true }`
- **list** → `:612-618`. Zwraca `success:true` także wtedy, gdy ustawione jest `error`.
  `{ success: true, scripts: { id: string|null, name: string, title: string|null, version: any|null, modified: any|null }[], count: number, source: 'internal_api', error?: string }`
- **errors** → `:339-344`: `{ success: true, has_errors: boolean, error_count: number, errors: PineMarker[] }`
- **console** → `:426`: `{ success: true, entries: { timestamp: string|null /*"HH:MM:SS" albo "YYYY-MM-DD HH:MM:SS"*/, type: 'info'|'error'|'compile'|'warning', message: string }[], entry_count: number }`

## 5. Screenshot (`src/core/capture.js:69-73`)

```ts
{ success: true, method: 'cdp', file_path: string /*absolutna: <repoCLI>/screenshots/<nazwa>.png*/, region?: string, waited_for_render: false, size_bytes: number }
```
- Regiony: `full` (domyślny; każda inna wartość też daje pełną stronę), `chart`, `strategy_tester`. Jeśli element regionu nie istnieje, zapisywana jest po cichu cała strona.
- `-o` to nazwa pliku bez `.png`; znaki `/`, `\` i `..` są sanityzowane.

## 6. Replay (`src/core/replay.js`). `current_date` w unix **sekundach**.

- **start** `[-d YYYY-MM-DD]` → `:57`: `{ success: true, replay_started: true, date: string /*'(first available)'*/, current_date: number|null }`
- **step** → `:75`: `{ success: true, action: 'step', current_date: number|null }`
- **stop** → `:101`/`:107`: `{ success: true, action: 'already_stopped' | 'replay_stopped' }`
- **status** → `:147`: `{ success: true, is_replay_available: boolean, is_replay_started: boolean, is_autoplay_started: boolean, replay_mode: any, current_date: number|null, autoplay_delay: number, position: number, realized_pnl: number }`
- **autoplay** `[-s ms]` → `:93`: `{ success: true, autoplay_active: boolean, delay_ms: number }`. To przełącznik (włącza albo wyłącza). Dozwolone prędkości (`:6`): `100, 143, 200, 300, 1000, 2000, 3000, 5000, 10000`.
- **trade ACTION** → `:125`: `{ success: true, action: 'buy'|'sell'|'close', position: number, realized_pnl: number }`

## 7. Draw (`src/core/drawing.js`)

- **shape** → `:44`: `{ success: true, shape: string, entity_id: string|null }`. Zwraca `success:true` nawet wtedy, gdy nie udało się ustalić id.
  - `--time` jest **wymagane** (unix s), również dla `horizontal_line`.
  - Drugi punkt jest używany tylko z `--price2`, i wtedy `--time2` też jest wymagane.
  - Typy z pomocy CLI: `horizontal_line` (domyślny), `trend_line`, `rectangle`, `text`. Wartość jest przekazywana dalej bez zmian, więc działa dowolna nazwa kształtu TradingView.
- **list** → `:56`: `{ success: true, count: number, shapes: { id: string, name: string }[] }`
- **get ID** → `:85`: `{ success: true, entity_id: string, available_methods?: string[], points?: { time: number /*unix s*/, price: number }[], points_error?: string, properties?: object, properties_error?: string, visible?: boolean, locked?: boolean, selectable?: boolean, name?: string }`
- **remove ID** → `:106`: `{ success: true, entity_id: string, removed: boolean, remaining_shapes: number }`
- **clear** → `:112`: `{ success: true, action: 'all_shapes_removed' }`

## 8. Alert (`src/core/alerts.js`)

- **list** → `:94`: `{ success: true, alert_count: number, source: 'internal_api', alerts: Alert[], error?: string }`
  `Alert = { alert_id: number, symbol: string, type: any, message: string, active: boolean, condition: object, resolution: string, created: any, last_fired: any, expiration: any }`
- **create** `-p` (wymagane) `-c` `-m` → `:22`
  - sukces (`:56`): `{ success: true, source: 'internal_api', symbol: string, price: number, condition: 'cross'|'greater'|'less', message: string, alert_id: number|null }`
  - porażka (`:27`, `:58`, `:60`): `{ success: false, source?: 'internal_api', error: string, response?: string }`, **exit 0**
  - Warunki wejściowe (`:12-16`): `crossing|cross` → cross; `greater_than|greater|above|>` → greater; `less_than|less|below|<` → less. Każda inna wartość daje cross.
- **delete** `--all` | `--id N` → `:125`: `{ success: true, source, deleted_count: number, alert_ids: number[] }`
  - porażka: `{ success: false, source, error: string, alert_ids?: number[] }` (`:108`, `:127`), exit 0

## 9. Watchlist (`src/core/watchlist.js`)

- **get** → `:109-115`: `{ success: true, count: number, source: 'dom_rows'|'empty'|'no_container'|'unknown', list_id?: any, list_name?: string, symbols: { symbol: string, last: string|null, change: string|null, change_percent: string|null, volume: string|null }[] }`. Wartości w wierszach to **stringi**.
- **add SYM** → `:157`: `{ success: boolean, symbol: string, added_as: string|null, action: 'added'|'not_verified' }`
- **add-bulk SYM...** → `:171`: `{ success: boolean, added: number, failed: number, results: { symbol: string, success: boolean, added_as?: string|null, error?: string }[] }`
- **remove SYM...**
  - `:233-237`: `{ success: true, removed: string[], skipped: string[], verified: boolean, list_id: any, list_name: string, api: 'rest' }`
  - lub `:193`: `{ success: false, removed: [], skipped: string[], error: string }`, exit 0

## 10. Layout (`src/core/ui.js`)

- **list** → `:126`: `{ success: true, layout_count: number, source?: 'internal_api', layouts: { id: any, name: string, symbol: string|null, resolution: string|null, modified: any|null }[], error?: string }`
- **switch NAME|ID** → `:169`: `{ success: true, layout: string, layout_id: any, source: 'internal_api', action: 'switched', unsaved_dialog_dismissed: boolean }`

## 11. Indicator

- **add NAME [-i json]** → `src/core/chart.js:135-142`. Może zwrócić `success:false` przy exit 0.
  `{ success: boolean, action: 'add', indicator: string, entity_id: string|null, new_study_count: number, inputs?: { applied: Record<string,any>, unknown_inputs?: string[] } | { error: string } }`
- **remove ID** → `chart.js:151`: `{ success: true, action: 'remove', entity_id: string }`
- **toggle ID** `[--visible|--hidden]` → `src/core/indicators.js:210`: `{ success: true, entity_id: string, visible: boolean }`
- **set ID -i json** → `indicators.js:191`: `{ success: true, entity_id: string, updated_inputs: Record<string,any> }`. Nieznane klucze są po cichu ignorowane.
- **get ID** → kształt taki sam jak `data indicator`.

## 12. UI (`src/core/ui.js`)

- **click** `-b -v` → `:28`: `{ success: true, clicked: { found: true, tag: string, text: string, aria_label: string|null, data_name: string|null } }`
  Wartości `by`: `aria-label | data-name | text` (domyślnie) `| class-contains`.
- **keyboard KEY** `[--ctrl --shift --alt --meta]` → `:193`: `{ success: true, key: string, modifiers: ('ctrl'|'shift'|'alt'|'meta')[] }`
- **hover** → `:225`: `{ success: true, hovered: { by: string, value: string, tag: string, x: number, y: number } }`
- **scroll [dir]** `[-a px]` → `:243`: `{ success: true, direction: string, amount: number }`. Kierunki: `up|down|left|right`.
- **find QUERY** `-s` → `:296`: `{ success: true, query: string, strategy: string, count: number, elements: { tag, text, aria_label: string|null, data_name: string|null, x, y, width, height: number, visible: boolean }[] /*≤20*/ }`
  Strategie: `text` (domyślnie) | `aria-label` | `css`.
- **eval EXPR** → `:301`: `{ success: true, result: any }`
- **type TEXT** → `:199`: `{ success: true, typed: string /*≤100 znaków*/, length: number }`
- **panel NAME [action]** → `:63`/`:96`: `{ success: true, panel: string, action: string, was_open: boolean, performed: 'opened'|'closed'|'none'|'already_open'|'already_closed'|'unknown' }`
  - Panele: `pine-editor`, `strategy-tester`, `watchlist`, `alerts`, `trading`.
  - Akcje: `open|close|toggle` (domyślnie toggle).
- **fullscreen** → `:110`: `{ success: true, action: 'fullscreen_toggled' }`
- **mouse X Y** `[--right] [--double]` → `:258`: `{ success: true, x: number, y: number, button: 'left'|'right', double_click: boolean }`

## 13. Pane (`src/core/pane.js`)

- **list** → `:68-75`: `{ success: true, layout: string, layout_name: string, chart_count: number, active_index: number|null, panes: ({ index: number, symbol: string, resolution: string|null } | { index: number, error: string })[] }`
- **layout CODE** → `:103-109`: `{ success: true, layout: string, layout_name: string, chart_count: number, panes: Pane[] }`
  - Kody (`:10-29`): `s, 2h, 2v, 2-1, 1-2, 3h, 3v, 3s, 4, 4h, 4v, 4s, 6, 8, 10, 12, 14, 16`.
  - Aliasy (`:87-92`): `single|1|1x1→s`, `2x1→2h`, `1x2→2v`, `2x2|grid|quad→4`, `3x1→3h`, `1x3→3v`.
- **focus N** → `:130`: `{ success: true, focused_index: number, total_panes: number }`
- **symbol N SYM** → `:155`: `{ success: true, index: number, symbol: string }`

## 14. Tab (`src/core/tab.js`)

- **list** → `:35`: `{ success: true, tab_count: number, tabs: { index: number, id: string, title: string, url: string, chart_id: string|null, is_chart: boolean }[] }`
- **new** → `:151-156`: `{ success: true, action: 'new_tab_opened', note: string, tab_count: number, tabs: Tab[] }`
- **close** → `:290`: `{ success: boolean, action: 'tab_closed', tabs_before: number, tabs_after: number }`. Rzuca, gdy otwarta jest tylko jedna karta.
- **switch N** → `:333`: `{ success: true, action: 'switched', index: number, tab_id: string, chart_id: string|null, visually_switched: true }`

---

## 15. Streamy (JSONL: jeden **zwarty** obiekt na linię na stdout, komunikaty na stderr)

**Wspólna pętla** (`src/core/stream.js:15-56`; używają jej quote, bars, values, lines, labels, tables i all):
- Każda linia ma postać `{ ...data, _ts: number /*unix ms*/, _stream: label }`.
- Linia jest emitowana tylko wtedy, gdy dane się zmieniły; wynik `null` jest pomijany.
- Przy błędach CDP/ECONNREFUSED pętla ponawia próbę po 2 s. Inne błędy trafiają na stderr, a stream działa dalej.
- Wartość `-i` **nie jest walidowana**.

| komenda | `_stream` | domyślnie (ms) | payload (oprócz `_ts`, `_stream`) |
|---|---|---|---|
| quote `:62-86` | `'quote'` | 300 | `{ symbol, time /*unix s*/, open, high, low, close, volume }` |
| bars `:90-116` | `'bars'` | 500 | `{ symbol, resolution: string, bar_time /*unix s*/, open, high, low, close, volume, bar_index: number }` |
| values `:120-150` | `'values'` | 500 | `{ symbol, study_count, studies: { name: string, values: Record<string, number> }[] }` (wartości to **liczby**, w odróżnieniu od komendy `values`) |
| lines `:154-196` | `'lines'` | 1000 | `{ symbol, study_count, studies: { study: string, levels: number[] }[] }` |
| labels `:200-238` | `'labels'` | 1000 | `{ symbol, study_count, studies: { study: string, labels: { text: string, price: number\|null }[] }[] }` |
| tables `:243-288` | `'tables'` | 2000 | `{ symbol, study_count, studies: { study: string, tables: { rows: string[][] }[] }[] }` (**tablica 2D**, inaczej niż `data tables`) |
| all `:295-334` | `'all-panes'` | 500 | `{ layout: string, pane_count, panes: ({ index, symbol, resolution, time, open, high, low, close, volume } \| { index, symbol, error: 'no bars' } \| { index, error })[] }` |

**ohlcv (multi-feed)** → `src/core/multi-feed.js:391-499`.
- Ten stream **nie dodaje** pól `_ts` ani `_stream`.
- Emituje jedną linię na każdy feed, który się zmienił:
  ```ts
  { pane_index: number, symbol: string, timeframe: string, bar_time: number /*unix s*/, open, high, low, close: number,
    volume: number, bar_index: number, tab_id: string /*CDP target id*/, observed_at: number /*unix ms*/ }
  ```
- **Specyfikacja feedu** (`parseFeedSpecs`, `:5-24`):
  - spec jest dzielony na **ostatnim** `@`;
  - symbol i timeframe muszą być niepuste;
  - duplikat `SYMBOL@TF` to błąd;
  - wymagany jest co najmniej jeden spec;
  - błąd specyfikacji kończy się exit 1, zanim stream wystartuje.
- **Dokładne dopasowanie.** Porównania `symbol === series.symbol()` i `timeframe === String(series.interval())` są dokładne. Spec musi być w kanonicznej formie TradingView (np. `CME_MINI:ES1!@1`); inaczej po ok. 15 s pojawia się „Timed out setting symbol”.
- **Interwał** (`:26-33`): domyślnie 250 ms; musi być liczbą całkowitą **≥ 100 ms**.
- **Przydział paneli** (`:87-208`):
  - najpierw ponownie używane są pasujące panele, potem wolne;
  - dalej layout jest rozszerzany w kolejności `s, 2h, 3h, 4, 6, 8, 10, 12, 14, 16`;
  - na końcu otwierane są nowe karty.
  - Panele i karty **zostają otwarte** po zakończeniu streamu.

---

## Korekty potwierdzone fixturami z żywego TradingView

Wynik zrzutu `tools/Capture-Fixtures.ps1` (TradingView Desktop, CLI `3d2cbae`) i testów integracyjnych. Te punkty zastępują opis powyżej.

- **`chart_type` / `chartType` / `type_num`** mogą przyjmować wartości spoza 0–9 (na testowym wykresie: `19`). `tv type` wtedy zwraca w `chart_type` **liczbę**, nie nazwę. Modele trzymają surowe `ChartTypeId`/`TypeNum`; `ChartType` (enum) jest tylko dla 0–9.
- **`range` → `bars_range`** to unix **sekundy** (czas pierwszego i ostatniego załadowanego baru), a nie indeksy barów.
- **`ohlcv --summary`**: klucz to `last_5_bars` (nie `last5_bars`).
- **`discover`**: klucze w `apis` są camelCase (`chartApi`, `chartWidgetCollection`, ...), `methodCount` też.
- **`values`**: `studies[].inputs` zawiera `text` – zaszyfrowane źródło skryptów Pine użytkownika, oraz `pineId` (`USER;...`). Skrypt zrzutu maskuje oba pola.
- **`data strategy|trades|equity`** bez strategii na wykresie: exit 0, `success:false`, `error: "No strategy found on chart..."`.
- **`data depth`** bez panelu DOM: exit 1, `{"success":false,"error":"DOM / Depth of Market panel not found."}`.
- **`search`**: wypisuje kompletny JSON, po czym Node na Windows kończy się asercją libuv (`UV_HANDLE_CLOSING`), kod wyjścia **0xC0000409** (`-1073740791`). Klient traktuje taki wynik jako sukces, gdy stdout zawiera poprawny JSON.
- **Status**: pole `update` bierze `git rev-parse HEAD` z **katalogu roboczego** procesu, więc klient uruchamia CLI w katalogu jego repozytorium.

### Dodatkowe korekty z etapu 3 (Pine, Indicators, Replay, mutacje wykresu)

- **`pine check`** też kończy się crashem Node (0xC0000409) po wypisaniu kompletnego JSON-a; klient go toleruje (jak przy `search`).
- **`pine new|set|get|compile|errors|console|save`** wymagają otwartego edytora Pine. Przy zamkniętym edytorze CLI zawodzi z `Could not open Pine Editor.` (exit 1) albo `Ensure Pine Editor is open`. Klient otwiera panel (`tv ui panel pine-editor open`) i ponawia komendę raz (`TvClientOptions.AutoOpenPineEditor`).
- **`pine compile`** nowego, niezapisanego skryptu klika `Pine Save` (`button_clicked: "Pine Save"`, `study_added: false`) i zostawia na ekranie modal „Save script”. Skrypt nie trafia na wykres ani do konta, dopóki ktoś nie zatwierdzi dialogu. `has_errors`/`errors` działają także w tym trybie (`severity` 8 = error).
- **`pine save`** nie został zrzucony (zapisuje skrypt na koncie); model oparty na kontrakcie.
- **`pine console`**: `entries[].timestamp` bywa `null`, a `message` to cały tekst panelu sklejony w jeden blok (może się powtarzać w kilku wpisach).
- **`pine list`**: `id` ma postać `USER;<base62>` (nie hex), `modified` to unix sekundy, `version` to string (`"3.0"`).
- **`pine analyze`** dla poprawnego skryptu: `issue_count: 0` plus `note`. Wykrywa m.in. indeks poza tablicą (error), pustą tablicę (warning), `strategy.entry` bez `strategy()` (error).
- **`pine check`** dla błędu: `errors[]` z `line/column/end_line/end_column/message`; komunikat bywa szablonem (`Undeclared identifier "{identifier}"`).
- **`indicator add`**: wymaga pełnej nazwy TradingView (`Relative Strength Index`, `Volume`). `Moving Average Exponential` nie utworzyło wskaźnika (`success:false`, `entity_id:null`, bez pola `error`). Przy `-i` odpowiedź zawiera `inputs.applied` i `inputs.unknown_inputs`. Identyfikatory wejść RSI to `length` i `source` (nie `in_N`).
- **`symbol X`**: `chart_ready` bywa `false`, mimo że symbol się zmienił (np. `NASDAQ:AAPL`); klient zwraca to bez wyjątku.
- **`replay start`** bez daty startuje od najstarszej dostępnej daty (na wykresie 5 min: 2021-10-10), `current_date` w unix sekundach. `position` i `realized_pnl` zostają `null` także po `trade buy` (API handlu replay ich nie raportuje).
- **`replay autoplay -s N`** zmienia opóźnienie na stałe (widoczne potem w `status.autoplay_delay`), a samo `autoplay` przełącza włączone/wyłączone.
- **`ui panel pine-editor close`** zgłasza `performed: "closed"`, ale nie zamyka edytora; zamyka go kliknięcie przycisku `aria-label=Close` (`ui click -b aria-label -v Close`).

### Dodatkowe korekty z etapu 4 (Drawings, Alerts, Watchlist, Layouts, Panes, Tabs, Screenshot, Ui)

- **Wartości liczbowe w opcjach**: używamy formy `--price=-5`, `--text=-x`, `--message=--x`, bo `parseArgs` upstreamu nie odróżnia wartości zaczynających się od `-` od opcji.
- **`draw shape`**: nie waliduje nazwy kształtu. Potwierdzone działanie: `horizontal_line, horizontal_ray, vertical_line, trend_line, ray, extended, arrow, rectangle, circle, ellipse, triangle, parallel_channel, fib_retracement, long_position, short_position, text, callout, note, price_label, flag`. `draw get` przyciąga `points[].time` do czasu najbliższego baru, a `properties` ma zestaw kluczy zależny od kształtu.
- **`alert list`**: `condition` to obiekt o zmiennym kształcie, `created`/`expiration` to ISO 8601 (`2026-10-10T20:34:16Z`), `last_fired` bywa `null`. `alert create` zwraca `condition` znormalizowane (`cross|greater|less`).
- **`alert delete --id N`** zgłasza `deleted_count: 1` także dla nieistniejącego id. `delete --all` kasuje WSZYSTKIE alerty konta i nie jest nigdzie wywoływane w testach na żywo.
- **`watchlist add`/`add-bulk`** nie weryfikują symbolu: `NOPE:NOSUCH123` zostaje dodane i raportowane jako sukces. `get` zwraca stringi wyświetlane (`"7,811.54"`, `"202.13 K"`); `source: "empty"` pojawia się, gdy panel watchlisty nie jest aktywnym widżetem prawego panelu.
- **`layout list`**: `id` to liczba, `modified` to data `YYYY-MM-DD`. `layout switch` zwraca `unsaved_dialog_dismissed: true`, gdy odrzucił pytanie o niezapisane zmiany.
- **`pane layout`**: po zmniejszeniu układu `panes` nadal zawiera nieaktualne panele (`chart_count` jest miarodajny), a `pane focus` zwraca `total_panes` liczone razem z nimi. Symbol bywa rozwiązywany do innej giełdy (`NASDAQ:AAPL` → `BATS:AAPL`).
- **`tab new`** i **`tab close`** również kończą się crashem Node (0xC0000409) po kompletnym JSON-ie. Nowa karta jest wstawiana na indeks 0 jako `New tab` (`is_chart: false`). `tab list` pomija karty niebędące wykresem ani `New tab` (np. „Stock Heatmap”).
- **Upstreamowy błąd `tab close`/`tab switch`**: po zamknięciu karty TradingView aktywuje sąsiednią (u nas „Stock Heatmap”), a `tab switch 0` zgłasza sukces, ale nie aktywuje karty z wykresem, bo każda karta raportuje `visibilityState: visible`. Karta z wykresem w tle sprawia, że `screenshot` wisi bez końca (klient kończy to `TvTimeoutException`). Karta wraca po kliknięciu w pasku kart powłoki. Dlatego testy na żywo nie zamykają kart.
- **`ui mouse X Y`** i współrzędne z `ui find`/`hover` są w pikselach CSS, nie w pikselach zrzutu ekranu (dla `devicePixelRatio` 1.25 zrzut jest większy o 25%).
- **`ui panel watchlist`** przełącza prawy panel; otwiera ten widżet, który był ostatnio aktywny (u nas „Fundamental Graphs”). `ui panel pine-editor close` nie zamyka edytora.
- **`ui eval`** zwraca `{"success":true}` bez pola `result` dla `undefined`.
- **`screenshot`**: zapisuje do `<repo CLI>/screenshots/` (`tvclient_fixture.png`), `-o` bez rozszerzenia.

### Dodatkowe korekty z etapu 5 (strumienie)

- **Format linii:** zwarty JSON, jedna linia na zmianę. `_stream` dla `stream all` to `all-panes`. `stream ohlcv` nie ma `_ts`/`_stream`, ma `observed_at` (ms) i `tab_id`.
- **`stream values`:** `studies[].values` zawiera liczby, ale też wewnętrzne klucze `_start` i `_end` (np. 0 i 400), nie tylko wartości wskaźnika. Wartości `null` odpowiadają NaN.
- **Pusty rynek:** poza sesją strumień wypisuje jeden snapshot i milczy (dedup po JSON-ie), więc na pierwszą linię trzeba czekać do kilku sekund (start `node` + połączenie CDP).
- **`stream ohlcv` wymaga dokładnej zgodności symbolu:** `NASDAQ:AAPL@15` zakończyło się `Timed out setting symbol for NASDAQ:AAPL@15.`, bo TradingView rozwiązuje ten symbol do `BATS:AAPL`. Użyj symbolu tak, jak go raportuje wykres (`tv pane list`).
- **Skutki uboczne multi-feed:** feedy przypisywane są do paneli, więc `stream ohlcv CME_MINI:ES1!@1 CME_MINI:ES1!@15` zmienia timeframe aktywnego panelu na 1, a przy okazji rozwinął dolny panel „Strategy Report”. Po zakończeniu zostaje to na wykresie (CLI o tym ostrzega na stderr).
- **Błąd po komunikatach postępu:** przy niepowodzeniu strumienia stderr to linie tekstu (`[stream:ohlcv] stopped. ...`), a dopiero potem JSON `{success:false,error}`. Klient wyciąga `error` z ostatniego obiektu JSON.
- **Brak fixtur dla pustych studiów:** `stream lines|labels|tables` zwróciły u testowego użytkownika `studies: []`; kształty zawartości pochodzą z kontraktu i testów ręcznych.
- **`data strategy|trades|equity` mają skutek uboczny w UI:** otwierają dolny panel (Strategy Tester), co zostaje po komendzie, nawet gdy na wykresie nie ma strategii. `unhidden_strategies` w odpowiedzi to ślad tego mechanizmu.
