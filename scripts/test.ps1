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
<<<<<<< HEAD

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
  control = "sign(-4) = -1, sign(0) = 0, sign(9) = 1|grade(95) = A, grade(72) = C, grade(10) = F|sumTo(100) = 5050|factorial(10) = 3628800|countEven(1..20) = 10|sumTo(1000000) = 500000500000|3...|2...|1...|Liftoff!"
  math   = "7 - 10 = -3|6 * 7 = 42|17 / 5 = 3 (whole number division)|17 % 5 = 2 (the rest)|abs(-12) = 12|max(3, 9) = 9|min(3, 9) = 3|square(-4) = 16|clamp(15, 0, 10) = 10|average(4, 10) = 7|isEven(10) = true|isEven(7) = false"
=======
# run aip in its own process so its stderr is plain text, not PowerShell error records
function AipBoth { (cmd /c "powershell -NoProfile -File `"$aip`" $($args -join ' ') 2>&1" | Out-String) }
function AipOut { (cmd /c "powershell -NoProfile -File `"$aip`" $($args -join ' ') 2>nul" | Out-String).Trim() }
function NewTempDir($prefix) {
  $d = Join-Path ([IO.Path]::GetTempPath()) ($prefix + [guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory $d | Out-Null; $d
>>>>>>> origin/master
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
foreach ($check in Get-ChildItem (Join-Path $root 'tests\checks\*.ps1') | Sort-Object Name) {
  try { . $check.FullName }
  catch { Report "check $($check.Name)" $false "threw: $_" }
  Set-Location $root
}
if ($failed -eq 0) { 'ALL PASSED' } else { "$failed FAILED" }
exit $failed
