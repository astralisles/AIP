# bootstrap: the native Windows compiler written in AIP (examples\aipc, `aipc native <dir> <out.exe>`) writes an .exe
# by itself (no C#, no linker) that prints what the cs back end prints.
$build = (& $aip build examples\aipc | Out-String).Trim()
$aipc = Join-Path $root 'examples\aipc\build\bin\windows-x64\aipc.exe'
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $aipc)) { Report 'bootstrap: aipc native' $false $build; return }
$tmp = NewTempDir 'aipc-native-'
foreach ($name in 'hello', 'control') {
  $exe = Join-Path $tmp "$name.exe"
  $said = (& $aipc native (Join-Path $root "examples\$name") $exe | Out-String).Trim()
  if (-not (Test-Path $exe)) { Report "bootstrap: aipc native builds $name" $false $said; continue }
  $run = (& $exe | Out-String).Trim() -replace "`r", ''
  $want = 'Hello, World!'
  if ($name -ne 'hello') { $want = (Get-Content (Join-Path $root "examples\$name\expected-output.txt") -Raw).Trim() -replace "`r", '' }
  $bytes = [IO.File]::ReadAllBytes($exe)
  Report "bootstrap: aipc native $name prints the expected output" ($run -eq $want -and $bytes[0] -eq 0x4D -and $bytes[1] -eq 0x5A) "got: $run"
}
$said = (& $aipc native (Join-Path $root 'examples\loops') (Join-Path $tmp 'loops.exe') | Out-String).Trim()
Report 'bootstrap: aipc native refuses an unsupported construct with native-unsupported' ($said.Contains('native-unsupported') -and -not (Test-Path (Join-Path $tmp 'loops.exe'))) $said
