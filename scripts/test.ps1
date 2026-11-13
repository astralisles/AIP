<<<<<<< HEAD
# Builds the compiler, builds and runs examples\hello, and checks that every
# project in tests\errors is rejected with the text in its expected.txt. Then the static analysis:
# tests\lint (aip lint must warn), tests\contracts (aip test must fail), every example must pass
# aip lint / aip test / aip fuzz, and the check-*.ps1 consistency gates must pass.
# Prints one PASS/FAIL line per test; exit code 1 if any test fails.
=======
# Test harness. Discovers tests from files, so adding a test never means editing this script
# (parallel branches used to conflict here):
#   - examples\<name>\expected-output.txt  -> `aip run examples\<name>` must print exactly these lines
#   - tests\errors\<case>\expected.txt     -> `aip check` must reject the project with this text
#   - tests\checks\*.ps1                   -> any other check; each file calls Report (see tests\checks\README.md)
# Prints one PASS/FAIL line per test; exit code = number of failures.
>>>>>>> origin/master
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
<<<<<<< HEAD
$out = (& $aip run examples\hello --target linux-x64 | Out-String).Trim()
Report 'hello: running a foreign target is refused' ($LASTEXITCODE -ne 0 -and $out.Contains('cannot-run-target')) $out
$out = (& $aip check (Join-Path $root 'tests\no-such-folder') | Out-String).Trim()
Report 'check: a missing project folder is refused' ($LASTEXITCODE -ne 0 -and $out.Contains('no-project')) $out

# static analysis: each tests\lint case must warn, each tests\contracts case must fail `aip test`
foreach ($suite in @(@{ Dir = 'lint'; Cmd = 'lint' }, @{ Dir = 'contracts'; Cmd = 'test' })) {
  foreach ($case in Get-ChildItem (Join-Path $root "tests\$($suite.Dir)") -Directory | Sort-Object Name) {
    $expected = (Get-Content (Join-Path $case.FullName 'expected.txt') -Raw).Trim()
    $out = (& $aip $suite.Cmd $case.FullName | Out-String).Trim()
    Report "$($suite.Dir): $($case.Name)" ($LASTEXITCODE -ne 0 -and $out.Contains($expected)) "expected: $expected`n      got: $out"
  }
}
# every example is lint-clean, its `example` lines and contracts hold, and damaged copies never crash the compiler
$projects = Get-ChildItem (Join-Path $root 'examples') -Directory | Where-Object { Get-ChildItem $_.FullName -Filter *.fn -File } | Sort-Object Name
foreach ($p in $projects) {
  foreach ($cmd in 'lint', 'test', 'fuzz') {
    $out = (& $aip $cmd $p.FullName | Out-String).Trim()
    Report "example $($p.Name): aip $cmd" ($LASTEXITCODE -eq 0) $out
  }
}
# consistency gates (each prints its own detail on failure)
foreach ($gate in 'check-error-coverage', 'check-externs', 'check-index', 'check-determinism') {
  $out = (& (Join-Path $PSScriptRoot "$gate.ps1") | Out-String).Trim()
  Report "gate: $gate" ($LASTEXITCODE -eq 0) $out
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
# std layout (T-0942): grouped one folder deep; no stale flat copies in the build output; the compiler rejects a misplaced file
$out = (& $aip std | Out-String)
$groups = @($out -split "`r?`n" | Where-Object { $_ -match '^\w+/$' })
$srcCount = @(Get-ChildItem compiler\std -Recurse -File -Filter *.fn).Count
$fnCount = @($out -split "`r?`n" | Where-Object { $_ -match '^  \w+\(' }).Count
Report 'std: `aip std` lists every std file under its group' ($LASTEXITCODE -eq 0 -and $groups.Count -ge 9 -and $fnCount -eq $srcCount -and @(Get-ChildItem compiler\std -File -Filter *.fn).Count -eq 0) "groups: $($groups -join ' '), listed $fnCount of $srcCount`n$out"
$bin = Join-Path $root 'compiler\Aip.Compiler\bin\Release\net10.0'
$copy = Join-Path ([IO.Path]::GetTempPath()) ('aip-stdlayout-' + [guid]::NewGuid().ToString('N'))
Copy-Item $bin $copy -Recurse
Move-Item (Join-Path $copy 'std\text\textLength.fn') (Join-Path $copy 'std\textLength.fn')
Move-Item (Join-Path $copy 'std\list\listGet.fn') (Join-Path $copy 'std\map\listGet.fn')
$out = (& (Join-Path $copy 'aip.exe') check examples\hello | Out-String).Trim()
Remove-Item -Recurse -Force $copy
Report 'std: misplaced std files are rejected (std-layout)' ($LASTEXITCODE -ne 0 -and $out.Contains('std-layout') -and $out.Contains('`textLength` must be in a group folder') -and $out.Contains('`listGet` in std/map/ must start with `map`')) $out
$out = (& (Join-Path $PSScriptRoot "compare-lexers.ps1") | Out-String).Trim()
Report "bootstrap: AIP lexer matches the C# lexer" ($LASTEXITCODE -eq 0) $out
=======
foreach ($check in Get-ChildItem (Join-Path $root 'tests\checks\*.ps1') | Sort-Object Name) {
  try { . $check.FullName }
  catch { Report "check $($check.Name)" $false "threw: $_" }
  Set-Location $root
}
>>>>>>> origin/master
if ($failed -eq 0) { 'ALL PASSED' } else { "$failed FAILED" }
exit $failed
