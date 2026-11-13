# Times `aip query` on generated projects of growing size, to show what "never stored" costs.
# Usage: scripts\bench-query.ps1 -WorkDir <empty folder> [-Sizes 10,100,1000,5000] [-Out result.txt]
param([Parameter(Mandatory)][string]$WorkDir, [int[]]$Sizes = @(10, 100, 1000, 5000), [string]$Out)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'aip.ps1') check (Join-Path $root 'examples\hello') | Out-Null   # builds the compiler
$exe = Join-Path $root 'compiler\Aip.Compiler\bin\Release\net10.0\aip.exe'
$lines = @('functions | query wall time (median of 5, ms) | note')
foreach ($n in $Sizes) {
  $dir = Join-Path $WorkDir "p$n"
  New-Item -ItemType Directory -Force $dir | Out-Null
  for ($i = 0; $i -lt $n; $i++) {
    $body = if ($i -eq 0) { 'printLine("hi")' } else { "f$($i - 1)(x)" }
    if ($i -eq 0) { $sig = "fn f0(x I64) -> Unit"; $eff = "effects terminal.write" } else { $sig = "fn f$i(x I64) -> Unit"; $eff = "effects terminal.write" }
    Set-Content (Join-Path $dir "f$i.fn") "$sig`n  `"Generated function $i.`"`n  $eff`nbody`n  $body" -Encoding ascii
  }
  Set-Content (Join-Path $dir 'main.fn') "fn main() -> Unit`n  `"Entry.`"`n  effects terminal.write`nbody`n  f$($n - 1)(1)" -Encoding ascii
  & $exe check $dir | Out-Null
  if ($LASTEXITCODE -ne 0) { $lines += "$n | generated project does not check"; continue }
  $times = 1..5 | ForEach-Object { (Measure-Command { & $exe query $dir callers f0 | Out-Null }).TotalMilliseconds }
  $lines += "$n | $([int](($times | Sort-Object)[2])) |"
}
$lines
if ($Out) { [IO.File]::WriteAllLines($Out, $lines) }
