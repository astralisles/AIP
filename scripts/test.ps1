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
if ($failed -eq 0) { 'ALL PASSED' } else { "$failed FAILED" }
exit $failed
