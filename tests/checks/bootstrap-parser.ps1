# bootstrap: the header parser written in AIP (examples\aipc) reads every header like the C# parser.
$out = (& (Join-Path $PSScriptRoot '..\..\scripts\compare-index.ps1') | Out-String).Trim()
Report "bootstrap: AIP parser reads headers like the C# parser" ($LASTEXITCODE -eq 0) $out
