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
# independent runs: start them as parallel jobs (6 at a time), then report in a fixed order
function Start-Limited($jobs, $block, $arguments) {
  $jobs.Value += Start-Job -ScriptBlock $block -ArgumentList $arguments
  while (@($jobs.Value | Where-Object State -eq 'Running').Count -ge 6) { $null = Wait-Job -Job $jobs.Value -Any }
}
$jobs = @()
$run = { param($aip, $dir, $cmd, $label) $o = (& $aip $cmd $dir | Out-String).Trim(); [pscustomobject]@{ Label = $label; Ok = ($LASTEXITCODE -eq 0); Out = $o } }
foreach ($p in $projects) {
  foreach ($cmd in 'lint', 'test', 'fuzz') { Start-Limited ([ref]$jobs) $run @($aip, $p.FullName, $cmd, "example $($p.Name): aip $cmd") }
}
# consistency gates (each prints its own detail on failure)
$gateRun = { param($script, $label) $o = (& $script | Out-String).Trim(); [pscustomobject]@{ Label = $label; Ok = ($LASTEXITCODE -eq 0); Out = $o } }
foreach ($gate in 'check-error-coverage', 'check-externs', 'check-index', 'check-determinism') {
  Start-Limited ([ref]$jobs) $gateRun @((Join-Path $root "scripts\$gate.ps1"), "gate: $gate")
}
foreach ($r in ($jobs | Receive-Job -Wait -AutoRemoveJob)) { Report $r.Label $r.Ok $r.Out }
