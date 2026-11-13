# Bootstrap check: the body parser written in AIP (examples\aipc, `aipc ast <file>`) must print exactly the
# dump the C# parser prints (`aip ast`) for every .fn file the C# parser accepts. Files the C# parser rejects are skipped.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$aip = Join-Path $PSScriptRoot 'aip.ps1'
$build = (& $aip build examples\aipc | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { "FAIL  aipc: does not build`n$build"; exit 1 }
$aipc = Join-Path $root 'examples\aipc\build\bin\windows-x64\aipc.exe'
$cs = Join-Path $root 'compiler\Aip.Compiler\bin\Release\net10.0\aip.exe'
$files = Get-ChildItem examples, tests, compiler\std -Recurse -Include *.fn | Where-Object { $_.FullName -notmatch '[\/]build[\/]' }
$names = @($files | ForEach-Object FullName)
$jobs = foreach ($k in 0..5) {
  $mine = @(for ($n = $k; $n -lt $names.Count; $n += 6) { $names[$n] })
  Start-Job -ArgumentList $aipc, $cs, ($mine -join "|") -ScriptBlock {
    param($aipc, $cs, $mine)
    $ErrorActionPreference = "Continue"
    foreach ($f in $mine.Split("|")) {
      $b = (& $cs ast $f 2>$null) -join "`n"
      if ($LASTEXITCODE -ne 0) { "SKIP  $f"; continue }
      $a = (& $aipc ast $f 2>$null) -join "`n"
      if ($a -ne $b) { "FAIL  ast differs: $f" }
    }
  }
}
$results = @($jobs | Receive-Job -Wait -AutoRemoveJob)
$skipped = @($results | Where-Object { $_ -like 'SKIP*' }).Count
$bad = @($results | Where-Object { $_ -like 'FAIL*' })
$bad
if ($bad.Count -eq 0) { "PASS  AIP body parser matches the C# parser on $($files.Count - $skipped) files ($skipped skipped: rejected by C#)" } else { "$($bad.Count) of $($files.Count) files differ" }
exit $bad.Count
