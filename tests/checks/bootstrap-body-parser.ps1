# bootstrap: the body parser written in AIP (examples\aipc, `aipc ast`) reads every body like the C# parser (`aip ast`).
$out = (& (Join-Path $PSScriptRoot '..\..\scripts\compare-ast.ps1') | Out-String).Trim()
Report "bootstrap: AIP parser reads bodies like the C# parser" ($LASTEXITCODE -eq 0) $out
