# Bootstrap check: the lexer written in AIP (examples\lexer) must print exactly the
# tokens the C# lexer prints (`aip tokens`) for every .fn and .const file in the repo.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$aip = Join-Path $PSScriptRoot 'aip.ps1'
$build = (& $aip build examples\lexer | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { "FAIL  lexer: does not build`n$build"; exit 1 }
$lexer = Join-Path $root 'examples\lexer\build\bin\windows-x64\lexer.exe'
$cs = Join-Path $root 'compiler\Aip.Compiler\bin\Release\net10.0\aip.exe'
$files = Get-ChildItem examples, tests, compiler\std -Recurse -Include *.fn, *.const | Where-Object { $_.FullName -notmatch '[\/]build[\/]' }
$bad = 0
foreach ($f in $files) {
  $a = (& $lexer $f.FullName) -join "`n"
  $b = (& $cs tokens $f.FullName) -join "`n"
  if ($a -ne $b) { $bad++; "FAIL  tokens differ: $($f.FullName)" }
}
if ($bad -eq 0) { "PASS  AIP lexer matches the C# lexer on $($files.Count) files" } else { "$bad of $($files.Count) files differ" }
exit $bad
