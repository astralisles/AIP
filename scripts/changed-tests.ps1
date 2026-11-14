# Decides which tests a change needs, from the files git says changed.
# Usage:  scripts\changed-tests.ps1 [-Base <ref>]     prints the plan (see scripts\test.ps1 -Changed to run it)
# Changed files = committed since the merge-base with Base (default: main, master, origin/main or origin/master,
# first that exists) + uncommitted + untracked.
# Rules (anything not listed runs everything, because a wrong skip is worse than a slow run):
#   docs\**, *.md, .gitignore, .gitattributes   -> no tests
#   examples\<name>\**                          -> that example (plus the inline hello/cache tests if <name> is hello)
#   tests\errors\<case>\**                      -> that error case
#   tests\checks\<file>.ps1                     -> that check
#   anything else (compiler\, scripts\, ...)    -> all tests
# Returns an object: All (bool), Examples, Errors, Checks (names), Files (the changed files), Inline (bool).
param([string]$Base)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
  if (-not $Base) {
    foreach ($c in 'main', 'master', 'origin/main', 'origin/master') {
      git rev-parse --verify --quiet "$c^{commit}" *> $null
      if ($LASTEXITCODE -eq 0) { $Base = $c; break }
    }
  }
  $files = @()
  if ($Base) {
    $mb = (git merge-base $Base HEAD 2>$null)
    if ($LASTEXITCODE -eq 0 -and $mb) { $files += @(git diff --name-only $mb HEAD) }
  }
  $files += @(git diff --name-only HEAD)
  $files += @(git ls-files --others --exclude-standard)
  $files = @($files | Where-Object { $_ } | ForEach-Object { $_ -replace '\\', '/' } | Sort-Object -Unique)

  $plan = [pscustomobject]@{ All = $false; Inline = $false; Examples = @(); Errors = @(); Checks = @(); Files = $files }
  if (-not $Base) { $plan.All = $true }   # cannot tell what changed
  foreach ($f in $files) {
    if ($f -match '^docs/' -or $f -match '\.md$' -or $f -in '.gitignore', '.gitattributes') { continue }
    elseif ($f -match '^examples/([^/]+)/') {
      $plan.Examples += $Matches[1]
      if ($Matches[1] -eq 'hello') { $plan.Inline = $true }
    }
    elseif ($f -match '^tests/errors/([^/]+)/') { $plan.Errors += $Matches[1] }
    elseif ($f -match '^tests/checks/([^/]+\.ps1)$') { $plan.Checks += $Matches[1] }
    else { $plan.All = $true }
  }
  $plan.Examples = @($plan.Examples | Sort-Object -Unique)
  $plan.Errors = @($plan.Errors | Sort-Object -Unique)
  $plan.Checks = @($plan.Checks | Sort-Object -Unique)
  $plan
} finally { Pop-Location }
