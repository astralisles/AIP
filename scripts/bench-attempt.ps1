# Benchmark helper: runs one `aip` command for a model that is writing a program, and logs the attempt.
# The log (not the model's own report) is the measurement: how many `check`/`run` attempts until the first
# clean one, and which error codes were hit. One line per attempt in <project>.attempts.log:
#   <time> | <verb> | exit <code> | <first error line or "ok">
# Usage: scripts\bench-attempt.ps1 <projectDir> spec|find|check|run [words...]
#        scripts\bench-attempt.ps1 -Summary <folder with *.attempts.log>     (one result line per task)
[CmdletBinding(PositionalBinding = $false)]
param([Parameter(Position = 0)][string]$Project, [Parameter(Position = 1)][string]$Verb,
  [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest, [string]$Summary)
$ErrorActionPreference = 'Continue'
$aip = Join-Path $PSScriptRoot 'aip.ps1'

if ($Summary) {
  "task | checks before first clean check | runs | error codes hit"
  foreach ($log in Get-ChildItem $Summary -Filter *.attempts.log | Sort-Object Name) {
    $lines = @(Get-Content $log.FullName)
    $checks = @($lines | Where-Object { $_ -match '\| (check|run) \|' })
    $firstOk = -1
    for ($i = 0; $i -lt $checks.Count; $i++) { if ($checks[$i] -match '\| exit 0 \|') { $firstOk = $i; break } }
    $before = if ($firstOk -lt 0) { "never clean ($($checks.Count) tries)" } else { "$firstOk" }
    $runs = @($lines | Where-Object { $_ -match '\| run \|' }).Count
    $codes = @($lines | Select-String 'error ([a-z][a-z0-9-]+)' | ForEach-Object { $_.Matches[0].Groups[1].Value } | Group-Object | ForEach-Object { "$($_.Name) x$($_.Count)" }) -join ', '
    if (-not $codes) { $codes = '-' }
    "$($log.BaseName -replace '\.attempts$', '') | $before | $runs | $codes"
  }
  exit 0
}

if (-not $Project -or $Verb -notin 'spec', 'find', 'check', 'run') {
  "usage: bench-attempt.ps1 <projectDir> spec|find|check|run [words...]"; exit 2
}
if (-not (Test-Path $Project)) { New-Item -ItemType Directory -Force $Project | Out-Null }
$dir = (Resolve-Path $Project).Path.TrimEnd('\')
$log = "$dir.attempts.log"
[string[]]$aipArgs = if ($Verb -eq 'spec') { @('spec') } else { @($Verb, $dir) + @($Rest | Where-Object { $_ }) }
$out = @(& $aip @aipArgs 2>&1 | ForEach-Object { "$_" })
$code = $LASTEXITCODE
$first = @($out | Where-Object { $Verb -in 'check', 'run' -and $_ -match '\berror\b' } | Select-Object -First 1)
$note = if ($first.Count -gt 0) { $first[0].Trim() } elseif ($code -ne 0) { 'failed' } else { 'ok' }
Add-Content -Path $log -Encoding utf8 -Value ("{0} | {1} | exit {2} | {3}" -f (Get-Date -Format 'HH:mm:ss'), $Verb, $code, $note)
$out
exit $code
