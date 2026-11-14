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
  $std = Join-Path $root 'compiler\std'
  $src += Get-ChildItem $std -Recurse -File
  $pkgs = Join-Path $root 'compiler\packages'
  if (Test-Path $pkgs) { $src += Get-ChildItem $pkgs -Recurse -File }
  $stale = [bool]($src | Where-Object { $_.LastWriteTimeUtc -gt $built } | Select-Object -First 1)
  # A moved, added or deleted std file keeps old timestamps: also rebuild when the file lists differ.
  if (-not $stale) {
    $copy = Join-Path (Split-Path $exe) 'std'
    $list = { param($d) if (Test-Path $d) { (Get-ChildItem $d -Recurse -File -Include *.fn,*.const | ForEach-Object { $_.FullName.Substring($d.Length) }) -join '|' } }
    $stale = (& $list $std) -ne (& $list $copy)
    if (-not $stale -and (Test-Path $pkgs)) { $stale = (& $list $pkgs) -ne (& $list (Join-Path (Split-Path $exe) 'packages')) }
  }
}
if ($stale) {
  $out = & dotnet build $proj -c Release -nologo -v q
  if ($LASTEXITCODE -ne 0) { $out | Select-String 'error' | Select-Object -First 20; exit 1 }
}
& $exe @args
exit $LASTEXITCODE
