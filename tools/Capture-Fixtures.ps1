<#
.SYNOPSIS
  Captures real `tv` CLI responses into tests/TradingView.Client.Tests/Fixtures.

.DESCRIPTION
  Runs `node <tradingview-cli>/src/cli/index.js <args>` directly (no cmd.exe, so `!`, `^`, `&` and quotes
  in arguments survive) and writes stdout to Fixtures/<area>/<name>.json (streams: .jsonl).
  By default only read-only commands run. -IncludeMutating also runs the scenarios that change the chart,
  alerts, indicators or replay state; they clean up after themselves, but they touch YOUR chart and account,
  so only use the switch deliberately.

  Works on Windows PowerShell 5.1 and PowerShell 7+.

.PARAMETER Area
  Capture only the given areas (e.g. Health). Default: all registered areas.

.PARAMETER IncludeMutating
  Also run commands that modify TradingView state.

.PARAMETER StreamSeconds
  How long each `stream *` command runs before it is stopped (default 5).

.PARAMETER NoSanitize
  Keep chart ids / CDP target ids as returned by TradingView (default: they are masked before committing).
#>
[CmdletBinding()]
param(
    [string[]] $Area,
    [switch] $IncludeMutating,
    [int] $StreamSeconds = 5,
    [switch] $NoSanitize
)

$ErrorActionPreference = 'Stop'
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
    param([string[]] $TvArgs, [string] $StdIn, [int] $StopAfterSeconds = 0)

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
    if ($StdIn) { $p.StandardInput.Write($StdIn) }
    $p.StandardInput.Close()
    $outTask = $p.StandardOutput.ReadToEndAsync()
    $errTask = $p.StandardError.ReadToEndAsync()

    if ($StopAfterSeconds -gt 0) {
        if (-not $p.WaitForExit($StopAfterSeconds * 1000)) {
            try { $p.Kill($true) } catch { $p.Kill() }
        }
    } else {
        $p.WaitForExit()
    }
    $p.WaitForExit()
    return [pscustomobject]@{ ExitCode = $p.ExitCode; StdOut = $outTask.Result; StdErr = $errTask.Result }
}

function Protect-Fixture([string] $text) {
    if ($NoSanitize) { return $text }
    $text = [regex]::Replace($text, '("target_id"\s*:\s*")[0-9A-Fa-f]{16,}(")', '${1}00000000000000000000000000000000${2}')
    $text = [regex]::Replace($text, '(tradingview\.com/chart/)[A-Za-z0-9]+', '${1}XXXXXXXX')
    # Account and layout names show up in UI button labels ("Logged in as NAME, Active layout: NAME").
    $text = [regex]::Replace($text, 'Logged in as [^\\"]+', 'Logged in as user')
    $text = [regex]::Replace($text, 'Active layout: [^\\"]+', 'Active layout: Layout')
    return $text
}

function Save-Fixture {
    param([string] $AreaName, [string] $Name, [string[]] $TvArgs, [string] $StdIn, [int] $StreamFor = 0)

    $isStream = $StreamFor -gt 0
    $ext = if ($isStream) { 'jsonl' } else { 'json' }
    $dir = Join-Path $fixturesRoot $AreaName.ToLowerInvariant()
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $file = Join-Path $dir "$Name.$ext"

    Write-Host ("tv " + ($TvArgs -join ' ')) -NoNewline
    $r = Invoke-Tv -TvArgs $TvArgs -StdIn $StdIn -StopAfterSeconds $StreamFor
    if (-not $isStream -and $r.ExitCode -ne 0) {
        Write-Host "  -> exit $($r.ExitCode): $($r.StdErr.Trim())" -ForegroundColor Yellow
        return $false
    }
    if ([string]::IsNullOrWhiteSpace($r.StdOut)) {
        Write-Host '  -> empty output, skipped' -ForegroundColor Yellow
        return $false
    }
    [System.IO.File]::WriteAllText($file, (Protect-Fixture $r.StdOut), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "  -> $($file.Substring($repoRoot.Length + 1))" -ForegroundColor Green
    return $true
}

# --- Capture catalogue -------------------------------------------------------------------------------------
# Each entry: Area, Name, Args, optional StdIn / Stream (seconds) / Mutating. Later stages append entries here.
$catalogue = @(
    @{ Area = 'Health'; Name = 'status';   Args = @('status') }
    @{ Area = 'Health'; Name = 'discover'; Args = @('discover') }
    @{ Area = 'Health'; Name = 'ui-state'; Args = @('ui-state') }
)

$status = Invoke-Tv -TvArgs @('status')
if ($status.ExitCode -ne 0) {
    throw "tv status failed (exit $($status.ExitCode)). Start TradingView with CDP enabled first (`tv launch`). $($status.StdErr)"
}

$captured = 0; $failed = 0
foreach ($entry in $catalogue) {
    if ($Area -and ($Area -notcontains $entry.Area)) { continue }
    if ($entry.Mutating -and -not $IncludeMutating) { continue }
    $stream = if ($entry.Stream) { $StreamSeconds } else { 0 }
    if (Save-Fixture -AreaName $entry.Area -Name $entry.Name -TvArgs $entry.Args -StdIn $entry.StdIn -StreamFor $stream) { $captured++ } else { $failed++ }
}

Write-Host "Captured $captured fixture(s), $failed failed." -ForegroundColor Cyan
if ($failed -gt 0) { exit 1 }
