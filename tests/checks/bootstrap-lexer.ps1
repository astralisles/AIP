# bootstrap: the lexer written in AIP (examples\lexer) prints the same tokens as the C# lexer.
$out = (& (Join-Path $PSScriptRoot '..\..\scripts\compare-lexers.ps1') | Out-String).Trim()
Report "bootstrap: AIP lexer matches the C# lexer" ($LASTEXITCODE -eq 0) $out
