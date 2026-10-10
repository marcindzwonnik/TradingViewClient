<#
.SYNOPSIS
  Captures real `tv` CLI responses into tests/TradingView.Client.Tests/Fixtures.

.DESCRIPTION
  Runs `node <tradingview-cli>/src/cli/index.js <args>` directly (no cmd.exe, so `!`, `^`, `&` and quotes
  in arguments survive) and writes stdout to Fixtures/<area>/<name>.json (streams: .jsonl).
  By default only read-only commands run. -IncludeMutating also runs scenarios that change the chart
  (symbol, timeframe, chart style, visible range), add and remove indicators, compile Pine scripts in the
  editor and run replay. Each scenario restores what it changed, but it does touch YOUR chart and account,
  so only use the switch deliberately. Not captured on purpose: `launch` (kills TradingView), `update`
  (changes the CLI checkout) and `pine save` (leaves a saved script in the account).

  Works on Windows PowerShell 5.1 and PowerShell 7+.

.PARAMETER Area
  Capture only the given areas (e.g. Chart,Pine). Default: all registered areas.

.PARAMETER IncludeMutating
  Also run scenarios that modify TradingView state.

.PARAMETER StreamSeconds
  How long each `stream *` command runs before it is stopped (default 5).

.PARAMETER NoSanitize
  Keep chart ids, CDP target ids, account and script names as returned (default: they are masked before committing).
#>
[CmdletBinding()]
param(
    [string[]] $Area,
    [switch] $IncludeMutating,
    [int] $StreamSeconds = 5,
    [switch] $NoSanitize
)

$ErrorActionPreference = 'Stop'
$Area = @($Area | ForEach-Object { $_ -split ',' } | Where-Object { $_ })  # -File passes 'A,B' as one string
$repoRoot = Split-Path -Parent $PSScriptRoot
$fixturesRoot = Join-Path $repoRoot 'tests/TradingView.Client.Tests/Fixtures'

function Find-TvCli {
    $tvCmd = Get-Command tv.cmd -ErrorAction SilentlyContinue
    if ($tvCmd) {
        $shimDir = Split-Path -Parent $tvCmd.Source
        $text = Get-Content -Raw -LiteralPath $tvCmd.Source
        if ($text -match '%dp0%[\\/]*(?<rel>[^"%]*index\.js)') {
            $script = [System.IO.Path]::GetFullPath((Join-Path $shimDir $Matches['rel']))
            $node = Join-Path $shimDir 'node.exe'
            if (-not (Test-Path -LiteralPath $node)) { $node = (Get-Command node -ErrorAction Stop).Source }
            if (Test-Path -LiteralPath $script) { return @{ Node = $node; Script = $script } }
        }
    }
    throw "Could not locate tradingview-cli. Make sure `tv` works (npm link) and tv.cmd is on PATH."
}

$cli = Find-TvCli
$cliRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $cli.Script))

function Invoke-Tv {
    param([string[]] $TvArgs, [string] $StdIn, [int] $StopAfterSeconds = 0, [int] $TimeoutSeconds = 120)

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $cli.Node
    $psi.WorkingDirectory = $cliRoot
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $psi.StandardOutputEncoding = $utf8
    $psi.StandardErrorEncoding = $utf8
    if ($psi.PSObject.Properties.Name -contains 'StandardInputEncoding') { $psi.StandardInputEncoding = $utf8 }

    # ArgumentList only exists on .NET Core; build a correctly quoted string for .NET Framework (PS 5.1).
    $allArgs = @($cli.Script) + $TvArgs
    if ($psi.PSObject.Properties.Name -contains 'ArgumentList') {
        foreach ($a in $allArgs) { [void] $psi.ArgumentList.Add($a) }
    } else {
        $psi.Arguments = ($allArgs | ForEach-Object {
            if ($_ -match '[\s"]' -or $_ -eq '') { '"' + ($_ -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"' } else { $_ }
        }) -join ' '
    }

    $p = [System.Diagnostics.Process]::Start($psi)
    if ($StdIn) {
        # Windows PowerShell 5.1 has no StandardInputEncoding: write UTF-8 bytes ourselves.
        $bytes = $utf8.GetBytes($StdIn)
        $p.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
        $p.StandardInput.BaseStream.Flush()
    }
    $p.StandardInput.Close()
    $outTask = $p.StandardOutput.ReadToEndAsync()
    $errTask = $p.StandardError.ReadToEndAsync()

    $limit = if ($StopAfterSeconds -gt 0) { $StopAfterSeconds } else { $TimeoutSeconds }
    $timedOut = -not $p.WaitForExit($limit * 1000)
    if ($timedOut) { try { $p.Kill($true) } catch { $p.Kill() } }
    $p.WaitForExit()
    return [pscustomobject]@{
        ExitCode = if ($timedOut -and $StopAfterSeconds -le 0) { -1 } else { $p.ExitCode }
        StdOut   = $outTask.Result
        StdErr   = $errTask.Result
    }
}

function Get-TvJson([string[]] $TvArgs) {
    $r = Invoke-Tv -TvArgs $TvArgs
    if ($r.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($r.StdOut)) { return $null }
    return $r.StdOut | ConvertFrom-Json
}

$script:scriptCounter = 0
function Protect-Fixture([string] $text, [string] $areaName, [string] $name) {
    if ($NoSanitize) { return $text }
    $text = [regex]::Replace($text, '("target_id"\s*:\s*")[0-9A-Fa-f]{16,}(")', '${1}00000000000000000000000000000000${2}')
    $text = [regex]::Replace($text, '(tradingview\.com/chart/)[A-Za-z0-9]+', '${1}XXXXXXXX')
    # Account and layout names show up in UI button labels ("Logged in as NAME, Active layout: NAME").
    $text = [regex]::Replace($text, 'Logged in as [^\\"]+', 'Logged in as user')
    $text = [regex]::Replace($text, 'Active layout: [^\\"]+', 'Active layout: Layout')
    # Private Pine scripts: encrypted source blobs in study inputs and script ids (hex or base62).
    $text = [regex]::Replace($text, '("text"\s*:\s*")[A-Za-z0-9]+_[^"]{100,}(")', '${1}REDACTED${2}')
    $text = [regex]::Replace($text, '(USER|ENC|STD|PUB)(;|%3B)[A-Za-z0-9]{16,}', '${1}${2}00000000000000000000000000000000')
    # Saved script names/titles belong to the user.
    if ($areaName -eq 'Pine' -and ($name -eq 'list' -or $name -eq 'open')) {
        $script:scriptCounter = 0
        $text = [regex]::Replace($text, '("(?:name|title)"\s*:\s*")[^"]*(")', {
            param($m)
            $script:scriptCounter++
            $m.Groups[1].Value + 'script-' + [int][math]::Ceiling($script:scriptCounter / 2) + $m.Groups[2].Value
        })
    }
    return $text
}

$script:captured = 0
$script:failed = 0

# Runs one tv command and stores its stdout as a fixture. Returns { Ok, ExitCode, Json }.
function Step {
    param(
        [string] $AreaName,
        [string] $Name,
        [string[]] $TvArgs,
        [string] $StdIn,
        [int] $StreamFor = 0,
        [switch] $AllowFailure
    )

    $isStream = $StreamFor -gt 0
    $ext = if ($isStream) { 'jsonl' } else { 'json' }
    $dir = Join-Path $fixturesRoot $AreaName.ToLowerInvariant()
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $file = Join-Path $dir "$Name.$ext"

    Write-Host ("tv " + (($TvArgs | ForEach-Object { if ($_.Length -gt 60) { $_.Substring(0, 57) + '...' } else { $_ } }) -join ' ')) -NoNewline
    $r = Invoke-Tv -TvArgs $TvArgs -StdIn $StdIn -StopAfterSeconds $StreamFor
    # Upstream quirk: on Windows some commands (e.g. `search`) print complete JSON and then Node dies with
    # a libuv assertion (exit code 0xC0000409). The output is valid, so accept it like the client does.
    if ($r.ExitCode -eq -1073740791 -and -not [string]::IsNullOrWhiteSpace($r.StdOut)) {
        Write-Host '  (node crashed on exit after printing output; accepted)' -NoNewline -ForegroundColor DarkYellow
        $r.ExitCode = 0
    }
    if (-not $isStream -and $r.ExitCode -ne 0) {
        $colour = if ($AllowFailure) { 'DarkGray' } else { 'Yellow' }
        Write-Host "  -> exit $($r.ExitCode): $($r.StdErr.Trim() -replace '\s+', ' ')" -ForegroundColor $colour
        if (-not $AllowFailure) { $script:failed++ }
        return [pscustomobject]@{ Ok = $false; ExitCode = $r.ExitCode; Json = $null }
    }
    if ([string]::IsNullOrWhiteSpace($r.StdOut)) {
        Write-Host '  -> empty output, skipped' -ForegroundColor Yellow
        $script:failed++
        return [pscustomobject]@{ Ok = $false; ExitCode = $r.ExitCode; Json = $null }
    }
    [System.IO.File]::WriteAllText($file, (Protect-Fixture $r.StdOut $AreaName $Name), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "  -> $($file.Substring($repoRoot.Length + 1))" -ForegroundColor Green
    $script:captured++
    $json = $null
    if (-not $isStream) { try { $json = $r.StdOut | ConvertFrom-Json } catch { } }
    return [pscustomobject]@{ Ok = $true; ExitCode = $r.ExitCode; Json = $json }
}

function Test-AreaSelected([string] $name) { return (-not $Area) -or ($Area -contains $name) }

# --- Read-only capture catalogue ---------------------------------------------------------------------------
# Each entry: Area, Name, Args, optional StdIn / Stream. Later stages append entries here.
$pineValid = "//@version=6`nindicator(`"TV Client Test`", overlay = true)`nlog.info(`"tvclient hello`")`nplot(close, `"Close`")`n"
$pineInvalid = "//@version=6`nindicator(`"Broken`")`nplot(undefined_variable)`n"
$pineIssues = "//@version=5`nindicator(`"Issues`")`na = array.new_float(3)`nx = array.get(a, 5)`ne = array.new_float(0)`ny = e.first()`nstrategy.entry(`"L`", strategy.long)`nplot(close)`n"

$catalogue = @(
    @{ Area = 'Health'; Name = 'status';   Args = @('status') }
    @{ Area = 'Health'; Name = 'discover'; Args = @('discover') }
    @{ Area = 'Health'; Name = 'ui-state'; Args = @('ui-state') }

    @{ Area = 'Chart'; Name = 'state';      Args = @('state') }
    @{ Area = 'Chart'; Name = 'symbol';     Args = @('symbol') }
    @{ Area = 'Chart'; Name = 'timeframe';  Args = @('timeframe') }
    @{ Area = 'Chart'; Name = 'type';       Args = @('type') }
    @{ Area = 'Chart'; Name = 'info';       Args = @('info') }
    @{ Area = 'Chart'; Name = 'search';     Args = @('search', 'AAPL') }
    @{ Area = 'Chart'; Name = 'range';      Args = @('range') }

    @{ Area = 'Data'; Name = 'quote';           Args = @('quote') }
    @{ Area = 'Data'; Name = 'ohlcv';           Args = @('ohlcv', '-n', '5') }
    @{ Area = 'Data'; Name = 'ohlcv-summary';   Args = @('ohlcv', '-n', '20', '--summary') }
    @{ Area = 'Data'; Name = 'values';          Args = @('values') }
    @{ Area = 'Data'; Name = 'lines';           Args = @('data', 'lines') }
    @{ Area = 'Data'; Name = 'lines-verbose';   Args = @('data', 'lines', '-v') }
    @{ Area = 'Data'; Name = 'labels';          Args = @('data', 'labels', '-n', '5') }
    @{ Area = 'Data'; Name = 'labels-verbose';  Args = @('data', 'labels', '-n', '3', '-v') }
    @{ Area = 'Data'; Name = 'tables';          Args = @('data', 'tables') }
    @{ Area = 'Data'; Name = 'boxes';           Args = @('data', 'boxes') }
    @{ Area = 'Data'; Name = 'boxes-verbose';   Args = @('data', 'boxes', '-v') }
    @{ Area = 'Data'; Name = 'strategy';        Args = @('data', 'strategy') }
    @{ Area = 'Data'; Name = 'trades';          Args = @('data', 'trades', '-n', '5') }
    @{ Area = 'Data'; Name = 'equity';          Args = @('data', 'equity') }
    @{ Area = 'Data'; Name = 'depth';           Args = @('data', 'depth'); AllowFailure = $true }
    # Entity id of the first study on the chart (resolved from `tv state` when this entry runs).
    @{ Area = 'Data'; Name = 'indicator';       Args = { $id = (Get-TvJson @('state')).studies[0].id; @('data', 'indicator', $id) } }

    @{ Area = 'Pine'; Name = 'list';            Args = @('pine', 'list') }
    @{ Area = 'Pine'; Name = 'analyze';         Args = @('pine', 'analyze'); StdIn = $pineValid }
    @{ Area = 'Pine'; Name = 'analyze-issues';  Args = @('pine', 'analyze'); StdIn = $pineIssues }
    @{ Area = 'Pine'; Name = 'check';           Args = @('pine', 'check'); StdIn = $pineValid }
    @{ Area = 'Pine'; Name = 'check-errors';    Args = @('pine', 'check'); StdIn = $pineInvalid }

    @{ Area = 'Replay'; Name = 'status-stopped'; Args = @('replay', 'status') }
)

# --- Mutating scenarios (each restores what it changed) ---------------------------------------------------
function Get-StudyIds { $s = Get-TvJson @('state'); if ($s) { return @($s.studies | ForEach-Object { $_.id }) } else { return @() } }

function Remove-NewStudies([string[]] $before) {
    foreach ($id in (Get-StudyIds)) {
        if ($before -notcontains $id) { [void](Invoke-Tv -TvArgs @('indicator', 'remove', $id)) }
    }
}

function Invoke-ChartScenario {
    $orig = Get-TvJson @('state')
    $range = Get-TvJson @('range')
    try {
        [void](Step Chart set-symbol @('symbol', 'NASDAQ:AAPL'))
        [void](Step Chart set-timeframe @('timeframe', '60'))
        [void](Step Chart set-type @('type', 'Candles'))
        $v = Get-TvJson @('range')
        if ($v) { [void](Step Chart set-range @('range', '--from', [string]($v.visible_range.from + 3600), '--to', [string]($v.visible_range.to - 3600))) }
        [void](Step Chart scroll @('scroll', '2026-10-01'))
    } finally {
        if ($orig) {
            [void](Invoke-Tv -TvArgs @('symbol', $orig.symbol))
            [void](Invoke-Tv -TvArgs @('timeframe', $orig.resolution))
            # The CLI can only set styles 0-9; restore any other style through the chart API.
            [void](Invoke-Tv -TvArgs @('ui', 'eval', "window.TradingViewApi._activeChartWidgetWV.value().setChartType($($orig.chartType))"))
        }
        if ($range) { [void](Invoke-Tv -TvArgs @('range', '--from', [string]$range.visible_range.from, '--to', [string]$range.visible_range.to)) }
    }
}

function Invoke-IndicatorScenario {
    $before = Get-StudyIds
    try {
        $add = Step Indicators add @('indicator', 'add', 'Volume')
        [void](Step Indicators add-unknown @('indicator', 'add', 'ZZZ Nonexistent Indicator 12345') -AllowFailure)
        $ema = Step Indicators add-with-inputs @('indicator', 'add', 'Relative Strength Index', '-i', '{"length":7,"no_such_input":1}')
        $id = $ema.Json.entity_id
        if ($id) {
            $info = Get-TvJson @('indicator', 'get', $id)
            $numeric = @($info.inputs | Where-Object { $_.value -is [ValueType] -and $_.value -isnot [bool] }) | Select-Object -First 1
            [void](Step Indicators get @('indicator', 'get', $id))
            if ($numeric) { [void](Step Indicators set @('indicator', 'set', $id, '-i', "{`"$($numeric.id)`":21}")) }
            [void](Step Indicators toggle-hidden @('indicator', 'toggle', $id, '--hidden'))
            [void](Step Indicators toggle-visible @('indicator', 'toggle', $id, '--visible'))
            [void](Step Indicators remove @('indicator', 'remove', $id))
        }
    } finally {
        Remove-NewStudies $before
    }
}

function Close-PineUi {
    # Compiling an unsaved script opens a "Save script" dialog; dismiss it, then close the editor.
    # (`ui panel pine-editor close` does not reliably close the editor, so click its Close button.)
    [void](Invoke-Tv -TvArgs @('ui', 'click', '-b', 'text', '-v', 'Cancel'))
    $ui = Get-TvJson @('ui-state')
    if ($ui -and $ui.pine_editor.open) { [void](Invoke-Tv -TvArgs @('ui', 'click', '-b', 'aria-label', '-v', 'Close')) }
}

function Invoke-PineScenario {
    $before = Get-StudyIds
    try {
        # The first `pine new` fails while the editor is closed ("Could not open Pine Editor"); open it and retry.
        $new = Step Pine new-failed @('pine', 'new', 'indicator') -AllowFailure
        if (-not $new.Ok) {
            [void](Invoke-Tv -TvArgs @('ui', 'panel', 'pine-editor', 'open'))
            Start-Sleep -Seconds 3
        }
        [void](Step Pine new @('pine', 'new', 'indicator'))
        [void](Step Pine set @('pine', 'set') -StdIn $pineValid)
        [void](Step Pine get @('pine', 'get'))
        [void](Step Pine compile @('pine', 'compile'))
        [void](Invoke-Tv -TvArgs @('ui', 'click', '-b', 'text', '-v', 'Cancel'))
        [void](Step Pine console @('pine', 'console'))
        [void](Step Pine errors @('pine', 'errors'))
        [void](Step Pine raw-compile @('pine', 'raw-compile'))
        [void](Invoke-Tv -TvArgs @('ui', 'click', '-b', 'text', '-v', 'Cancel'))
        [void](Step Pine set-invalid @('pine', 'set') -StdIn $pineInvalid)
        [void](Step Pine compile-errors @('pine', 'compile'))
        [void](Invoke-Tv -TvArgs @('ui', 'click', '-b', 'text', '-v', 'Cancel'))
        [void](Step Pine errors-present @('pine', 'errors'))
        [void](Step Pine new-strategy @('pine', 'new', 'strategy'))
        $list = Get-TvJson @('pine', 'list')
        if ($list -and $list.scripts.Count -gt 0) { [void](Step Pine open @('pine', 'open', $list.scripts[0].name)) }
    } finally {
        Remove-NewStudies $before
        Close-PineUi
    }
}

function Invoke-ReplayScenario {
    try {
        [void](Step Replay start @('replay', 'start'))
        [void](Step Replay step @('replay', 'step'))
        [void](Step Replay status-started @('replay', 'status'))
        [void](Step Replay trade-buy @('replay', 'trade', 'buy'))
        [void](Step Replay step-2 @('replay', 'step'))
        [void](Step Replay status-in-position @('replay', 'status'))
        [void](Step Replay trade-close @('replay', 'trade', 'close'))
        [void](Step Replay autoplay-on @('replay', 'autoplay', '-s', '1000'))
        [void](Step Replay autoplay-off @('replay', 'autoplay'))
        [void](Step Replay stop @('replay', 'stop'))
        [void](Step Replay stop-again @('replay', 'stop'))
    } finally {
        [void](Invoke-Tv -TvArgs @('replay', 'stop'))
    }
}

# --- Run ---------------------------------------------------------------------------------------------------
$status = Invoke-Tv -TvArgs @('status')
if ($status.ExitCode -ne 0) {
    throw "tv status failed (exit $($status.ExitCode)). Start TradingView with CDP enabled first (`tv launch`). $($status.StdErr)"
}

foreach ($entry in $catalogue) {
    if (-not (Test-AreaSelected $entry.Area)) { continue }
    $tvArgs = if ($entry.Args -is [scriptblock]) { & $entry.Args } else { $entry.Args }
    $stream = if ($entry.Stream) { $StreamSeconds } else { 0 }
    [void](Step $entry.Area $entry.Name $tvArgs -StdIn $entry.StdIn -StreamFor $stream -AllowFailure:([bool]$entry.AllowFailure))
}

if ($IncludeMutating) {
    $stateBefore = Get-TvJson @('state')
    if (Test-AreaSelected 'Chart')      { Invoke-ChartScenario }
    if (Test-AreaSelected 'Indicators') { Invoke-IndicatorScenario }
    if (Test-AreaSelected 'Pine')       { Invoke-PineScenario }
    if (Test-AreaSelected 'Replay')     { Invoke-ReplayScenario }

    $stateAfter = Get-TvJson @('state')
    if ($stateBefore -and $stateAfter) {
        $same = ($stateBefore.symbol -eq $stateAfter.symbol) -and ($stateBefore.resolution -eq $stateAfter.resolution) -and
                ($stateBefore.chartType -eq $stateAfter.chartType) -and
                ((@($stateBefore.studies | ForEach-Object { $_.id }) -join ',') -eq (@($stateAfter.studies | ForEach-Object { $_.id }) -join ','))
        if ($same) { Write-Host 'Chart state restored (symbol, timeframe, style, studies).' -ForegroundColor Green }
        else { Write-Host "WARNING: chart state differs from before the scenarios: $($stateBefore | ConvertTo-Json -Compress -Depth 4) -> $($stateAfter | ConvertTo-Json -Compress -Depth 4)" -ForegroundColor Red }
    }
}

Write-Host "Captured $script:captured fixture(s), $script:failed failed." -ForegroundColor Cyan
if ($script:failed -gt 0) { exit 1 }
