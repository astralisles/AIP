# Test harness. Discovers tests from files, so adding a test never means editing this script
# (parallel branches used to conflict here):
#   - examples\<name>\expected-output.txt  -> `aip run examples\<name>` must print exactly these lines
#   - tests\errors\<case>\expected.txt     -> `aip check` must reject the project with this text
#   - tests\checks\*.ps1                   -> any other check; each file calls Report (see tests\checks\README.md)
# Prints one PASS/FAIL line per test; exit code = number of failures.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$aip = Join-Path $PSScriptRoot 'aip.ps1'
$failed = 0
function Report($name, $ok, $detail) {
  if ($ok) { "PASS  $name" } else { "FAIL  $name`n      $detail"; $script:failed++ }
}
# run aip in its own process so its stderr is plain text, not PowerShell error records
function AipBoth { (cmd /c "powershell -NoProfile -File `"$aip`" $($args -join ' ') 2>&1" | Out-String) }
function AipOut { (cmd /c "powershell -NoProfile -File `"$aip`" $($args -join ' ') 2>nul" | Out-String).Trim() }
function NewTempDir($prefix) {
  $d = Join-Path ([IO.Path]::GetTempPath()) ($prefix + [guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory $d | Out-Null; $d
}

foreach ($file in Get-ChildItem (Join-Path $root 'examples\*\expected-output.txt') | Sort-Object FullName) {
  $name = $file.Directory.Name
  $expected = @(Get-Content $file.FullName | Where-Object { $_ -ne '' }) -join '|'
  $lines = @(& $aip run "examples\$name")
  Report "example $name`: output is as expected" ($LASTEXITCODE -eq 0 -and ($lines -join '|') -eq $expected) "expected: $expected`n      got: $($lines -join '|')"
}
foreach ($case in Get-ChildItem (Join-Path $root 'tests\errors') -Directory | Sort-Object Name) {
  $expected = (Get-Content (Join-Path $case.FullName 'expected.txt') -Raw).Trim()
  $out = (& $aip check $case.FullName | Out-String).Trim()
  Report "error: $($case.Name)" ($LASTEXITCODE -ne 0 -and $out.Contains($expected)) "expected: $expected`n      got: $out"
}
foreach ($check in Get-ChildItem (Join-Path $root 'tests\checks\*.ps1') | Sort-Object Name) {
  try { . $check.FullName }
  catch { Report "check $($check.Name)" $false "threw: $_" }
  Set-Location $root
}
if ($failed -eq 0) { 'ALL PASSED' } else { "$failed FAILED" }
exit $failed
