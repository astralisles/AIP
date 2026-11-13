# Builds the compiler, builds and runs examples\hello, and checks that every
# project in tests\errors is rejected with the text in its expected.txt.
# Prints one PASS/FAIL line per test; exit code 1 if any test fails.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$aip = Join-Path $PSScriptRoot 'aip.ps1'
$failed = 0
function Report($name, $ok, $detail) {
  if ($ok) { "PASS  $name" } else { "FAIL  $name`n      $detail"; $script:failed++ }
}

$out = (& $aip build examples\hello | Out-String).Trim()
Report 'hello: builds' ($LASTEXITCODE -eq 0) $out
$exe = Join-Path $root 'examples\hello\build\bin\windows-x64\hello.exe'
if (Test-Path $exe) {
  $run = (& $exe | Out-String).Trim()
  Report 'hello: prints "Hello, World!"' ($run -eq 'Hello, World!') "got: $run"
} else { Report 'hello: prints "Hello, World!"' $false 'no program was built' }

$out = (& $aip run examples\hello | Out-String).Trim()
Report 'hello: `aip run` prints only "Hello, World!"' ($LASTEXITCODE -eq 0 -and $out -eq 'Hello, World!') "got: $out"

$out = (& $aip build examples\hello --target wasm | Out-String).Trim()
Report 'hello: wasm is refused with a clear message' ($LASTEXITCODE -ne 0 -and $out.Contains('unsupported-target')) $out

foreach ($case in Get-ChildItem (Join-Path $root 'tests\errors') -Directory | Sort-Object Name) {
  $expected = (Get-Content (Join-Path $case.FullName 'expected.txt') -Raw).Trim()
  $out = (& $aip check $case.FullName | Out-String).Trim()
  Report "error: $($case.Name)" ($LASTEXITCODE -ne 0 -and $out.Contains($expected)) "expected: $expected`n      got: $out"
}
if ($failed -eq 0) { 'ALL PASSED' } else { "$failed FAILED" }
exit $failed
