# Gate: compiler\std\*.fn (the declared standard library) and the places that implement it must agree.
#  1. every std function has a C# template in CsBackend.cs `Externs`, and every template has a std header
#  2. every pure std function is implemented by the `aip test` interpreter (Interp.cs), and vice versa
#  3. effects: a template that touches the console, files or program arguments must be declared with an effect,
#     and a function declared with an effect must really touch one (so `pure` is never a lie)
# Usage: scripts\check-externs.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$src = Join-Path $root 'compiler\Aip.Compiler'
# The templates are split by family: CsBackend.Externs.*.cs and Interp.Externs.*.cs
$cs = (Get-ChildItem $src -Filter 'CsBackend*.cs' | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$interp = (Get-ChildItem $src -Filter 'Interp.Externs.*.cs' | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$templates = @{}
foreach ($m in [regex]::Matches($cs, '\bd\["(\w+)"\] = "((?:[^"\\]|\\.)*)";')) { $templates[$m.Groups[1].Value] = $m.Groups[2].Value }
$std = @{}
foreach ($f in Get-ChildItem (Join-Path $root 'compiler\std') -Recurse -Filter *.fn) {
  $lines = Get-Content $f.FullName
  $eff = 'pure'
  foreach ($l in $lines) { if ($l -match '^  effects (.+)$') { $eff = $Matches[1].Trim() } }
  $std[[IO.Path]::GetFileNameWithoutExtension($f.Name)] = @{ Effects = $eff; Extern = ($lines -contains 'extern'); First = $lines[0] }
}
$interpNames = @{}
foreach ($m in [regex]::Matches($interp, 'case ((?:"\w+"(?: or )?)+):')) {
  foreach ($n in [regex]::Matches($m.Groups[1].Value, '"(\w+)"')) { $interpNames[$n.Groups[1].Value] = $true }
}
$problems = @()
foreach ($n in $std.Keys | Sort-Object) {
  if (-not $std[$n].Extern) { $problems += "std/$n.fn is not an extern header" }
  if ($std[$n].First -notmatch "^fn $n\(") { $problems += "std/$n.fn: the first line does not declare fn $n(" }
  if (-not $templates.ContainsKey($n)) { $problems += "std/$n.fn has no template in CsBackend.cs Externs" }
}
foreach ($n in $templates.Keys | Sort-Object) { if (-not $std.ContainsKey($n)) { $problems += "CsBackend.cs Externs has $n but there is no std/$n.fn" } }
$touches = 'System\.Console|System\.IO|Rt\.Args|Rt\.Write|Rt\.Tcp|Rt\.Udp|Environment'
foreach ($n in $templates.Keys | Sort-Object) {
  if (-not $std.ContainsKey($n)) { continue }
  $impure = $templates[$n] -match $touches
  $pure = $std[$n].Effects -eq 'pure'
  if ($impure -and $pure) { $problems += "std/$n.fn is declared pure but its C# template touches the outside world" }
  if (-not $impure -and -not $pure) { $problems += "std/$n.fn declares effects ($($std[$n].Effects)) but its C# template touches nothing" }
  if ($pure -and -not $interpNames.ContainsKey($n)) { $problems += "std/$n.fn is pure but Interp.cs (aip test) does not implement it" }
  if (-not $pure -and $interpNames.ContainsKey($n)) { $problems += "Interp.cs implements the effectful $n; aip test must refuse it" }
}
foreach ($n in $interpNames.Keys | Sort-Object) { if (-not $std.ContainsKey($n)) { $problems += "Interp.cs implements $n but there is no std/$n.fn" } }
if ($problems) { $problems; exit 1 }
"std library is consistent: $($std.Count) functions, $($templates.Count) C# templates, $($interpNames.Count) interpreter implementations"
exit 0

