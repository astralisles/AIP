# spec and find: `aip spec` prints a one-page spec; `aip find` searches project and std functions.
$out = (& $aip spec | Out-String)
Report 'spec: `aip spec` prints a one-page spec' ($LASTEXITCODE -eq 0 -and $out.Contains('AIP spec') -and @($out -split "`r?`n").Count -le 40) "lines: $(@($out -split "`r?`n").Count)"
$out = (& $aip find examples\shapes area | Out-String).Trim()
Report 'find: `aip find` ranks the matching project function first' ($LASTEXITCODE -eq 0 -and $out.StartsWith('project | area(shape Shape)')) $out
$out = (& $aip find examples\hello write file | Out-String).Trim()
Report 'find: std functions are found by effect or doc' ($LASTEXITCODE -eq 0 -and $out.Contains('fileWrite(')) $out
