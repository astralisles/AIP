# bootstrap: the AIP header parser (examples\aipc, `aipc index`) must end on a malformed header, not loop forever.
# An unclosed parameter list used to run past the last token for ever and held a build or test run until it was killed.
$build = (& $aip build examples\aipc | Out-String).Trim()
$aipc = Join-Path $root 'examples\aipc\build\bin\windows-x64\aipc.exe'
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $aipc)) { Report 'bootstrap: aipc index ends on a malformed header' $false $build; return }
$tmp = NewTempDir 'aipc-hang-'
$cases = @{ 'unclosed-params' = "fn broken(a Int`n  `"x`"`n  pure`nextern`n"; 'dangling-param' = "fn broken(a Int, b`n  `"x`"`n  pure`nextern`n" }
foreach ($name in $cases.Keys) {
  $file = Join-Path $tmp "$name.fn"
  [IO.File]::WriteAllText($file, $cases[$name])
  $p = Start-Process $aipc -ArgumentList "index `"$file`"" -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $tmp "$name.out")
  $ended = $p.WaitForExit(10000)
  if (-not $ended) { $p.Kill() }
  Report "bootstrap: aipc index ends on a malformed header ($name)" $ended 'aipc index did not end within 10 s'
}
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue

# Every real std header must also end: a type the AIP parser does not know yet (the hang that stalled six tickets) shows up here.
$hung = @()
foreach ($f in Get-ChildItem (Join-Path $root 'compiler\std') -Recurse -Include *.fn, *.const) {
  $p = Start-Process $aipc -ArgumentList "index `"$($f.FullName)`"" -PassThru -WindowStyle Hidden -RedirectStandardOutput "$env:TEMP\aipc-hang-out.txt"
  if (-not $p.WaitForExit(10000)) { $p.Kill(); $hung += $f.Name }
}
Report 'bootstrap: aipc index ends on every std header' ($hung.Count -eq 0) ("hangs on: " + ($hung -join ', '))
