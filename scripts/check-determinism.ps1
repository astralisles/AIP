# Gate: the compiler is deterministic. For each project it writes the generated C# and the index twice
# (`aip emit`, into two fresh folders, the second time from a different working directory) and
# compares every file byte for byte (SHA-256). Nothing is built, so it takes a few seconds.
# Usage: scripts\check-determinism.ps1 [project dirs...]   (default: every folder in examples\)
param([string[]]$Projects)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$aip = Join-Path $PSScriptRoot 'aip.ps1'
if (-not $Projects) { $Projects = Get-ChildItem (Join-Path $root 'examples') -Directory | ForEach-Object { $_.FullName } }
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('aip-determinism-' + [guid]::NewGuid())
New-Item -ItemType Directory $tmp | Out-Null
$failed = 0
try {
  foreach ($p in $Projects) {
    $label = Split-Path $p -Leaf
    $a = Join-Path $tmp "$label-a"; $b = Join-Path $tmp "$label-b"
    & $aip emit $p --out $a | Out-Null
    if ($LASTEXITCODE -ne 0) { "FAIL  determinism $label`: emit failed"; $failed++; continue }
    Push-Location $tmp
    & $aip emit $p --out $b | Out-Null
    Pop-Location
    $ha = Get-ChildItem $a -File | Sort-Object Name | ForEach-Object { "$($_.Name) $((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" }
    $hb = Get-ChildItem $b -File | Sort-Object Name | ForEach-Object { "$($_.Name) $((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" }
    if (($ha -join "`n") -ceq ($hb -join "`n")) { "PASS  determinism $label ($(@($ha).Count) files identical)" }
    else { "FAIL  determinism $label`: the two runs differ"; Compare-Object $ha $hb; $failed++ }
  }
} finally { Remove-Item $tmp -Recurse -Force }
exit $failed
