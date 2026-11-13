# Runs the aip compiler, building it first if needed.
# Usage: scripts\aip.ps1 build examples\hello [--target linux-x64]
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root 'compiler\Aip.Compiler'
$exe = Join-Path $proj 'bin\Release\net10.0\aip.exe'
$out = & dotnet build $proj -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { $out | Select-String 'error' | Select-Object -First 20; exit 1 }
& $exe @args
exit $LASTEXITCODE
