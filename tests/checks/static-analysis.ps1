# Static analysis (T-0933): command-line errors, tests\lint (aip lint must warn), tests\contracts (aip test must fail),
# every example passes aip lint / aip test / aip fuzz, and the scripts\check-*.ps1 consistency gates pass.
$out = (& $aip run examples\hello --target linux-x64 | Out-String).Trim()
Report 'hello: running a foreign target is refused' ($LASTEXITCODE -ne 0 -and $out.Contains('cannot-run-target')) $out
$out = (& $aip check (Join-Path $root 'tests\no-such-folder') | Out-String).Trim()
Report 'check: a missing project folder is refused' ($LASTEXITCODE -ne 0 -and $out.Contains('no-project')) $out

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
  $out = (& (Join-Path $root "scripts\$gate.ps1") | Out-String).Trim()
  Report "gate: $gate" ($LASTEXITCODE -eq 0) $out
}
