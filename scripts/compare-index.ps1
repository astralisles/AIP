# Bootstrap check: the parser written in AIP (examples\aipc, `aipc index <files>`) must read every
# function and constant header the same way as the C# compiler: same signature and effects as `aip index`
# for every example project, and as `aip std` for the standard library.
# Usage: scripts\compare-index.ps1   (prints PASS/FAIL lines; exit code = number of mismatches)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$aip = Join-Path $PSScriptRoot 'aip.ps1'
$build = (& $aip build examples\aipc | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { "FAIL  aipc: does not build`n$build"; exit 1 }
$aipc = Join-Path $root 'examples\aipc\build\bin\windows-x64\aipc.exe'
$cs = Join-Path $root 'compiler\Aip.Compiler\bin\Release\net10.0\aip.exe'

# The source files of a folder in the order the C# compiler loads them (ordinal by full path, no build output).
function SourceFiles($dir) {
  [string[]]$files = Get-ChildItem $dir -Recurse -File -Include *.fn, *.const |
    Where-Object { $_.FullName -notmatch '[\\/]build[\\/]' } | ForEach-Object FullName
  [Array]::Sort($files, [StringComparer]::Ordinal)
  $files
}
function Compare-Lines($name, [string[]]$expected, [string[]]$actual) {
  # the order of lines is not part of the comparison (`aip index` sorts by name, `aip std` groups)
  [Array]::Sort($expected, [StringComparer]::Ordinal); [Array]::Sort($actual, [StringComparer]::Ordinal)
  $e = $expected -join "`n"; $a = $actual -join "`n"
  if ($e -eq $a) { $script:count += $expected.Count; return }
  $script:bad++
  "FAIL  index differs: $name"
  for ($i = 0; $i -lt [Math]::Max($expected.Count, $actual.Count); $i++) {
    if ($expected[$i] -ne $actual[$i]) { "      C#:   $($expected[$i])`n      AIP:  $($actual[$i])" }
  }
}
$bad = 0; $count = 0

foreach ($project in Get-ChildItem examples -Directory | Where-Object { Get-ChildItem $_.FullName -Recurse -File -Include *.fn, *.const | Where-Object { $_.FullName -notmatch "[\/]build[\/]" } } | Sort-Object Name) {  # skips leftover folders holding only build output
  $expected = @(& $cs index $project.FullName | ForEach-Object { $_ -replace ' \| checked, not proved$', '' })
  if ($LASTEXITCODE -ne 0) { $bad++; "FAIL  $($project.Name): aip index failed: $expected"; continue }
  $actual = @(& $aipc index @(SourceFiles $project.FullName))
  Compare-Lines $project.Name $expected $actual
}

[string[]]$expected = @(& $cs std | Where-Object { $_.StartsWith('  ') } | ForEach-Object { $_.Trim() })
[string[]]$actual = @(& $aipc index @(SourceFiles (Join-Path $root 'compiler\std')))
Compare-Lines 'std' $expected $actual

if ($bad -eq 0) { "PASS  AIP parser reads all $count headers like the C# parser" } else { "$bad project(s) differ" }
exit $bad
