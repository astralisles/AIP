# Gate: every error code the compiler can report must have a case in tests\errors (or tests\contracts for `aip test`) whose
# expected.txt contains "error <code>". Also fails if an expected.txt names a code the compiler
# no longer has. Exit code 1 on any gap. Usage: scripts\check-error-coverage.ps1 [-List]
param([switch]$List)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$src = Get-ChildItem (Join-Path $root 'compiler\Aip.Compiler') -Filter *.cs -File | ForEach-Object { Get-Content $_.FullName -Raw }
$text = $src -join "`n"
$codes = @{}
# Error(line, "code", ...)  /  Err(line, "code", ...)  /  Err("code", ...)  /  new Diagnostic(file, line, "code", ...)
foreach ($pattern in @('\bErr(?:or)?\((?:[^,"()]|\([^()]*\))+,\s*"([a-z]+(?:-[a-z]+)*)"',
                       '\bErr\(\s*"([a-z]+(?:-[a-z]+)*)"',
                       'new Diagnostic\((?:[^,"()]|\([^()]*\))+,(?:[^,"()]|\([^()]*\))+,\s*"([a-z]+(?:-[a-z]+)*)"',
                       'Failed\("([a-z]+(?:-[a-z]+)*)"',
                       'error ([a-z]+(?:-[a-z]+)+):')) {
  foreach ($m in [regex]::Matches($text, $pattern)) { $codes[$m.Groups[1].Value] = $true }
}
$expected = @{}
# codes that scripts\test.ps1 checks itself (command line and back end errors, not `aip check` errors)
foreach ($m in [regex]::Matches((Get-Content (Join-Path $PSScriptRoot 'test.ps1') -Raw), "Contains\('([a-z]+(?:-[a-z]+)+)'\)")) { $expected[$m.Groups[1].Value] = $true }
# internal-error guards that cannot be triggered from a project
foreach ($c in 'backend-failed', 'backend-missing-extern') { $expected[$c] = $true }
foreach ($f in @(Get-ChildItem (Join-Path $root 'tests\errors') -Recurse -Filter expected.txt) + @(Get-ChildItem (Join-Path $root 'tests\contracts') -Recurse -Filter expected.txt)) {
  foreach ($m in [regex]::Matches((Get-Content $f.FullName -Raw), 'error ([a-z]+(?:-[a-z]+)*):')) { $expected[$m.Groups[1].Value] = $true }
}
$untested = @($codes.Keys | Where-Object { -not $expected.ContainsKey($_) } | Sort-Object)
$stale = @($expected.Keys | Where-Object { -not $codes.ContainsKey($_) } | Sort-Object)
if ($List) { "codes in the compiler: $($codes.Count)"; ($codes.Keys | Sort-Object) -join ' ' }
if ($untested) { "error codes without a tests\errors case: $($untested -join ', ')" }
if ($stale) { "tests\errors names codes the compiler does not have: $($stale -join ', ')" }
# lint warnings: each `Warn(..., "code", ...)` in Lint.cs needs a tests\lint\<code> case, and the reverse
$warn = @{}
foreach ($m in [regex]::Matches((Get-Content (Join-Path $root 'compiler\Aip.Compiler\Lint.cs') -Raw), 'Warn\(\w+,\s*[^,]+,\s*"([a-z]+(?:-[a-z]+)*)"')) { $warn[$m.Groups[1].Value] = $true }
$haveLint = @(Get-ChildItem (Join-Path $root 'tests\lint') -Directory | ForEach-Object Name)
$noLint = @($warn.Keys | Where-Object { $haveLint -notcontains $_ } | Sort-Object)
$staleLint = @($haveLint | Where-Object { -not $warn.ContainsKey($_) } | Sort-Object)
if ($noLint) { "lint warnings without a tests\lint case: $($noLint -join ', ')" }
if ($staleLint) { "tests\lint names warnings the compiler does not have: $($staleLint -join ', ')" }
if ($untested -or $stale -or $noLint -or $staleLint) { exit 1 }
"all $($codes.Count) error codes and $($warn.Count) lint warnings have a test case"
exit 0
