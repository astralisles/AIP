# bootstrap: the staged bootstrap test (scripts\bootstrap.ps1): stage 1 builds, and reads its own source like the C# compiler.
$out = (& (Join-Path $PSScriptRoot '..\..\scripts\bootstrap.ps1') | Out-String).Trim()
Report "bootstrap: stage 1 reads its own source" ($LASTEXITCODE -eq 0) $out
