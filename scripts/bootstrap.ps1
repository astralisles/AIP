# Bootstrap test: how far can the compiler written in AIP (examples\aipc) reproduce itself?
#   stage 1: the C# compiler builds aipc.
#   self-read: stage 1 reads its OWN source (tokens, header index, bodies) exactly like the C# compiler does.
#   stage 2: stage 1 builds aipc from its own source. Not possible until aipc has a checker and emitter
#            (docs/design/bootstrapping.md, items 2-5); reported as SKIP, and it turns into a check once `aipc build` exists.
# Usage: scripts\bootstrap.ps1   (prints PASS/FAIL/SKIP lines; exit code = number of failures)
$ErrorActionPreference = 'Continue'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$aip = Join-Path $PSScriptRoot 'aip.ps1'
$bad = 0
function Fail($m) { $script:bad++; "FAIL  $m" }

$build = (& $aip build examples\aipc | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { Fail "stage 1: C# compiler does not build aipc`n$build"; exit 1 }
"PASS  stage 1: C# compiler built aipc"
$aipc = Join-Path $root 'examples\aipc\build\bin\windows-x64\aipc.exe'
$cs = Join-Path $root 'compiler\Aip.Compiler\bin\Release\net10.0\aip.exe'

[string[]]$files = Get-ChildItem examples\aipc -Recurse -File -Include *.fn, *.const |
  Where-Object { $_.FullName -notmatch '[\/]build[\/]' } | ForEach-Object FullName
[Array]::Sort($files, [StringComparer]::Ordinal)

$n = 0
foreach ($f in $files) {
  $a = (& $aipc tokens $f) -join "`n"; $b = (& $cs tokens $f) -join "`n"
  if ($a -ne $b) { Fail "self-read: tokens differ: $f" }
  $b = (& $cs ast $f 2>$null) -join "`n"
  if ($LASTEXITCODE -eq 0) {
    $a = (& $aipc ast $f 2>$null) -join "`n"
    if ($a -ne $b) { Fail "self-read: ast differs: $f" }
  }
  $n++
}
$expected = @(& $cs index examples\aipc | ForEach-Object { $_ -replace ' \| checked, not proved$', '' }) | Sort-Object { $_ } -CaseSensitive
$actual = @(& $aipc index $files) | Sort-Object { $_ } -CaseSensitive
if (($expected -join "`n") -ne ($actual -join "`n")) { Fail "self-read: index differs from 'aip index examples\aipc'" }
if ($bad -eq 0) { "PASS  self-read: stage 1 reads its own $n source files like the C# compiler (tokens, index, bodies)" }

# stage 2 needs `aipc build`; the usage line lists the commands aipc has.
$usage = (& $aipc 2>&1 | Out-String)
if ($usage -match 'build') { Fail "stage 2: aipc now has a build command; extend scripts\bootstrap.ps1 to compare stage 2 with stage 1" }
else { "SKIP  stage 2: aipc cannot compile yet (needs checker and emitter, see docs/design/bootstrapping.md)" }
exit $bad
