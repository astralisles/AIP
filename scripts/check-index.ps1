# Gate: the index (`aip index`, and build\index.txt after a build) must match the function headers
# in the source. The expected index is derived here, independently of the compiler, straight from each .fn
# file: its `fn ...` line, its `pure` / `effects ...` line, sorted by function name.
# Usage: scripts\check-index.ps1 [project dirs...]   (default: every folder in examples\)
param([string[]]$Projects)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$aip = Join-Path $PSScriptRoot 'aip.ps1'
# (folders with no .fn source, such as a leftover build\ folder, are not projects)
if (-not $Projects) { $Projects = Get-ChildItem (Join-Path $root 'examples') -Directory | Where-Object { Get-ChildItem $_.FullName -Filter *.fn -File } | ForEach-Object { $_.FullName } }
$failed = 0
foreach ($p in $Projects) {
  $rows = @()
  foreach ($f in Get-ChildItem $p -Recurse -Include *.fn, *.const -File | Where-Object { $_.FullName -notmatch '[\\/]build[\\/]' }) {
    $lines = Get-Content $f.FullName
    # a .const file starts `const name Type`, a .fn file `fn name(...)`
    if ($f.Extension -eq '.const') { $sig = $lines[0].Trim(); $name = ($sig -split ' ')[1] }
    else { $sig = $lines[0].Substring(3).Trim(); $name = ($sig -split '\(')[0] }
    $eff = 'pure'
    foreach ($l in $lines) { if ($l -match '^  effects (.+)$') { $eff = ($Matches[1].Split(',') | ForEach-Object { $_.Trim() }) -join ', ' } }
    $rows += [pscustomobject]@{ Name = $name; Line = "$sig | $eff | checked, not proved" }
  }
  # ordinal order (uppercase before lowercase), as the compiler sorts
  $names = [string[]]@($rows | ForEach-Object { $_.Name }); $keys = [string[]]@($rows | ForEach-Object { $_.Line })
  if ($rows.Count -gt 1) { [Array]::Sort($names, $keys, [StringComparer]::Ordinal) }
  $expected = ($keys -join "`n") + "`n"
  $got = (& $aip index $p) -join "`n"
  $got += "`n"
  $ok = ($got -ceq $expected)
  $label = Split-Path $p -Leaf
  if ($ok) { "PASS  index $label ($($rows.Count) functions)" }
  else { "FAIL  index $label`n--- expected`n$expected--- got`n$got"; $failed++ }
  $file = Join-Path $p 'build\index.txt'
  if (Test-Path $file) {
    $disk = (Get-Content $file -Raw) -replace "`r`n", "`n"
    if ($disk -cne $expected) { "FAIL  build\index.txt of $label is stale or differs from the headers"; $failed++ }
  }
}
exit $failed
