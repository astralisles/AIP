# Runs the aip compiler, building it first if needed.
# Usage: scripts\aip.ps1 build examples\hello [--target linux-x64]
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root 'compiler\Aip.Compiler'
$exe = Join-Path $proj 'bin\Release\net10.0\aip.exe'
# Speed: `dotnet build` costs ~2.5 s even when nothing changed, so only run it when the
# compiler's sources (or the std library it copies) are newer than the built aip.exe.
$stale = -not (Test-Path $exe)
if (-not $stale) {
  $built = (Get-Item $exe).LastWriteTimeUtc
  $src = @(Get-ChildItem $proj -File | Where-Object { $_.Extension -in '.cs', '.csproj' })
  $src += Get-ChildItem (Join-Path $root 'compiler\std') -Recurse -File
  $stale = [bool]($src | Where-Object { $_.LastWriteTimeUtc -gt $built } | Select-Object -First 1)
}
if ($stale) {
  $out = & dotnet build $proj -c Release -nologo -v q
  if ($LASTEXITCODE -ne 0) { $out | Select-String 'error' | Select-Object -First 20; exit 1 }
}
& $exe @args
exit $LASTEXITCODE
