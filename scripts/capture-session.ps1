# Runs each command line from the repo root and writes "> command" followed by its
# real output to a log file. Used to record evidence of compiler runs.
# Usage: scripts\capture-session.ps1 -Out log.txt -Commands 'scripts\aip.ps1 check examples\hello', '...'
param([Parameter(Mandatory)][string]$Out, [Parameter(Mandatory)][string[]]$Commands)
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$log = New-Object System.Collections.Generic.List[string]
foreach ($c in $Commands) {
  $log.Add("> $c")
  $global:LASTEXITCODE = 0
  $text = (Invoke-Expression "& $c" | Out-String).TrimEnd()
  if ($text) { foreach ($l in $text -split "`r?`n") { $log.Add($l) } }
  if ($LASTEXITCODE -ne 0) { $log.Add("(exit code $LASTEXITCODE)") }
  $log.Add('')
}
[IO.File]::WriteAllLines($Out, $log)
