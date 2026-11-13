# Adds the header line `fails` to every function `aip lint` reports as `hidden-failure`, and repeats
# until the project is clean (marking a function makes its unmarked callers the next ones reported).
# Use after a merge where one branch added `fails` to std or project functions and another added callers.
# Usage: scripts\fix-hidden-failure.ps1 examples\aipc [examples\scaling ...]   (prints one line per file changed)
param([Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)][string[]]$Projects)
$ErrorActionPreference = 'Continue'
$aip = Join-Path $PSScriptRoot 'aip.ps1'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$changed = 0
foreach ($project in $Projects) {
  $dir = (Resolve-Path $project).Path
  for ($round = 0; $round -lt 50; $round++) {
    $names = @(& $aip lint $dir 2>&1 | ForEach-Object { "$_" } |
      Select-String 'warning hidden-failure: `([A-Za-z0-9_]+)` calls' |
      ForEach-Object { $_.Matches[0].Groups[1].Value } | Sort-Object -Unique)
    if ($names.Count -eq 0) { break }
    $fixed = 0
    foreach ($name in $names) {
      $file = Get-ChildItem $dir -Recurse -File -Filter "$name.fn" |
        Where-Object { $_.FullName -notmatch '[\\/]build[\\/]' } | Select-Object -First 1
      if (-not $file) { "SKIP  no file for $name in $project"; continue }
      $text = [IO.File]::ReadAllText($file.FullName)
      $nl = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
      # The effects line (`pure` or `effects ...`) is the first header line that starts with one of those words.
      $new = [regex]::Replace($text, '(?m)^(\s+)(pure|effects\b[^\r\n]*)(\r?\n)', "`$1`$2`$3`$1fails$nl", 1)
      if ($new -eq $text) { "SKIP  no pure/effects line in $($file.FullName)"; continue }
      [IO.File]::WriteAllText($file.FullName, $new, $utf8)
      "fails  $($file.FullName.Substring((Get-Location).Path.Length + 1))"
      $fixed++; $changed++
    }
    if ($fixed -eq 0) { break }
  }
}
"$changed file(s) changed"
