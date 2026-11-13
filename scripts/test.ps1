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

$tmp = Join-Path ([IO.Path]::GetTempPath()) ('aip-writefile-' + [guid]::NewGuid())
New-Item -ItemType Directory $tmp | Out-Null
$out = (& $aip build examples\writefile | Out-String).Trim()
Report 'writefile: builds' ($LASTEXITCODE -eq 0) $out
Push-Location $tmp
$run = (& (Join-Path $root 'examples\writefile\build\bin\windows-x64\writefile.exe') | Out-String).Trim()
$file = Join-Path $tmp 'greeting.txt'
$content = if (Test-Path $file) { (Get-Content $file -Raw).Trim() } else { '' }
Pop-Location
Remove-Item $tmp -Recurse -Force
Report 'writefile: writes greeting.txt with "Hello from AIP!"' ($content -eq 'Hello from AIP!' -and $run -eq 'Wrote greeting.txt') "file: $content, output: $run"


$expectedOut = @{
  adding = "2 + 3 = 5|-7 + 10 = 3|1000000 + 2345678 = 3345678|1 + 2 + 3 = 6|Total: 65"
  types  = "Int:  42|Text: hello|Bool: true|Bool from a comparison (count > 40): true|Bool from and/or/not: true|Text equality: true|Ann is 30. Adult: yes|Tim is 12. Adult: no"
  shapes = "circle of radius 2 at 0,0, area 12|4 by 5 rectangle at 1,2, area 20|(skipping an empty shape)|dot at 7,8, area 0"
  collections = "List size: 2, first: pear|Map size: 2|Ann is 30|Has Bob: false|Updated Ann: 31, original still 30|Keys: 2, first key: Ann|After remove: 1|Set size (3, 5, 3): 2|Has 5: true, has 4: false|Set items: 2, first: 3|Map of lists: apple"
  math   = "7 - 10 = -3|6 * 7 = 42|17 / 5 = 3 (whole number division)|17 % 5 = 2 (the rest)|abs(-12) = 12|max(3, 9) = 9|min(3, 9) = 3|square(-4) = 16|clamp(15, 0, 10) = 10|average(4, 10) = 7|isEven(10) = true|isEven(7) = false"
}
foreach ($name in $expectedOut.Keys | Sort-Object) {
  $lines = @(& $aip run "examples\$name")
  Report "example $name`: output is as expected" ($LASTEXITCODE -eq 0 -and ($lines -join '|') -eq $expectedOut[$name]) "got: $($lines -join '|')"
}
foreach ($case in Get-ChildItem (Join-Path $root 'tests\errors') -Directory | Sort-Object Name) {
  $expected = (Get-Content (Join-Path $case.FullName 'expected.txt') -Raw).Trim()
  $out = (& $aip check $case.FullName | Out-String).Trim()
  Report "error: $($case.Name)" ($LASTEXITCODE -ne 0 -and $out.Contains($expected)) "expected: $expected`n      got: $out"
}
# run aip in its own process so its stderr is plain text, not PowerShell error records
function AipBoth { (cmd /c "powershell -NoProfile -File `"$aip`" $($args -join ' ') 2>&1" | Out-String) }
function AipOut { (cmd /c "powershell -NoProfile -File `"$aip`" $($args -join ' ') 2>nul" | Out-String).Trim() }
# logging and debugging: logLine goes to stderr only; --verbose logs compiler steps; crashes are reported cleanly
$stdout = AipOut run examples\logging
Report 'logging: stdout has only the normal output' ($stdout -eq 'Hello from the logging example') "got: $stdout"
$all = AipBoth run examples\logging --verbose
Report 'logging: logLine and --verbose go to stderr' ($all.Contains('debug: starting') -and $all.Contains('[aip') -and $all.Contains('checked: 0 error(s)')) $all
$crash = AipBoth run examples\crash
Report 'crash: run-time error is reported with exit code 1' ($LASTEXITCODE -eq 1 -and $crash.Contains('runtime error: KeyNotFoundException') -and -not $crash.Contains(' at Fn.')) $crash
$env:AIP_TRACE = '1'
$crash = AipBoth run examples\crash
Remove-Item Env:\AIP_TRACE
Report 'crash: AIP_TRACE=1 prints the stack trace' ($crash.Contains('at Fn.main')) $crash
# profiling: AIP_PROFILE writes JSON (time, memory, per-function calls); AIP_CALLS writes one JSON line per call with arguments
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("aip-prof-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $tmp | Out-Null
$env:AIP_PROFILE = Join-Path $tmp 'profile.json'; $env:AIP_CALLS = Join-Path $tmp 'calls.jsonl'; $env:AIP_CALLS_MAX = '1'
$null = AipBoth run examples\adding
Remove-Item Env:\AIP_PROFILE, Env:\AIP_CALLS, Env:\AIP_CALLS_MAX
$p = try { Get-Content (Join-Path $tmp 'profile.json') -Raw | ConvertFrom-Json } catch { $null }
$c = @(try { Get-Content (Join-Path $tmp 'calls.jsonl') } catch { @() })
Report 'profile: AIP_PROFILE and AIP_CALLS write valid JSON' ($p -and $p.schema -eq 1 -and $p.functions.Count -gt 0 -and $p.maxCallDepth -ge 1 -and $c.Count -eq 2 -and $c[1].Contains('truncated')) "profile: $p calls: $c"
Remove-Item -Recurse -Force $tmp
$out = (& (Join-Path $PSScriptRoot "compare-lexers.ps1") | Out-String).Trim()
Report "bootstrap: AIP lexer matches the C# lexer" ($LASTEXITCODE -eq 0) $out
if ($failed -eq 0) { 'ALL PASSED' } else { "$failed FAILED" }
exit $failed
