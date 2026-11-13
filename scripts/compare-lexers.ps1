# Bootstrap check: the lexer written in AIP (examples\aipc, `aipc tokens <file>`) must print exactly the
# tokens the C# lexer prints (`aip tokens`) for every .fn and .const file in the repo.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$aip = Join-Path $PSScriptRoot 'aip.ps1'
$build = (& $aip build examples\aipc | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { "FAIL  aipc: does not build`n$build"; exit 1 }
$lexer = Join-Path $root 'examples\aipc\build\bin\windows-x64\aipc.exe'
$cs = Join-Path $root 'compiler\Aip.Compiler\bin\Release\net10.0\aip.exe'
$files = Get-ChildItem examples, tests, compiler\std -Recurse -Include *.fn, *.const | Where-Object { $_.FullName -notmatch '[\/]build[\/]' }
# one process pair per file is slow: compare in 6 parallel jobs, each over its share of the files
$names = @($files | ForEach-Object FullName)
$jobs = foreach ($k in 0..5) {
  $mine = @(for ($n = $k; $n -lt $names.Count; $n += 6) { $names[$n] })
  Start-Job -ArgumentList $lexer, $cs, ($mine -join "|") -ScriptBlock {
    param($lexer, $cs, $mine)
    foreach ($f in $mine.Split("|")) {
      $a = (& $lexer tokens $f) -join "`n"
      $b = (& $cs tokens $f) -join "`n"
      if ($a -ne $b) { "FAIL  tokens differ: $f" }
    }
  }
}
$results = @($jobs | Receive-Job -Wait -AutoRemoveJob)
$results
$bad = $results.Count
if ($bad -eq 0) { "PASS  AIP lexer matches the C# lexer on $($files.Count) files" } else { "$bad of $($files.Count) files differ" }
exit $bad
