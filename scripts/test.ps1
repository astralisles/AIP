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

$out = (& $aip query examples\hello callers greeting | Out-String).Trim()
Report 'hello: query callers greeting -> main' ($LASTEXITCODE -eq 0 -and $out.StartsWith('main()')) $out
$out = (& $aip query examples\hello fn nope | Out-String).Trim()
Report 'hello: query of an unknown function fails clearly' ($LASTEXITCODE -ne 0 -and $out.Contains('no-such-function')) $out

# The source cache (build/sources.cache) must never give a stale answer: edit, add and delete files between queries.
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("aip-cache-test-" + [guid]::NewGuid())
Copy-Item (Join-Path $root 'examples\hello') $tmp -Recurse
Remove-Item (Join-Path $tmp 'build') -Recurse -Force -ErrorAction SilentlyContinue
$out = (& $aip query $tmp fn greeting | Out-String)
Report 'cache: first query creates build/sources.cache' ($LASTEXITCODE -eq 0 -and (Test-Path (Join-Path $tmp 'build\sources.cache'))) $out
$g = Join-Path $tmp 'greeting.fn'
[IO.File]::WriteAllText($g, ([IO.File]::ReadAllText($g) -replace 'A friendly greeting', 'A warm greeting'))
$out = (& $aip query $tmp fn greeting | Out-String)
Report 'cache: an edited file is re-read' ($out.Contains('A warm greeting') -and -not $out.Contains('A friendly')) $out
[IO.File]::WriteAllText((Join-Path $tmp 'shout.fn'), "fn shout(t Text) -> Text`n  `"Loud.`"`n  pure`nbody`n  `"{t}!`"`n")
$out = (& $aip query $tmp list | Out-String)
Report 'cache: a new file is seen (rejected as unused by main)' ($out.Contains('`shout` is never used')) $out
Remove-Item (Join-Path $tmp 'shout.fn')
$out = (& $aip query $tmp list | Out-String)
Report 'cache: a deleted file is gone' ($LASTEXITCODE -eq 0 -and -not $out.Contains('shout(')) $out
[IO.File]::WriteAllText((Join-Path $tmp 'build\sources.cache'), 'garbage')
$out = (& $aip query $tmp callers greeting | Out-String).Trim()
Report 'cache: a damaged cache is ignored' ($LASTEXITCODE -eq 0 -and $out.StartsWith('main()')) $out
Remove-Item $tmp -Recurse -Force

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
