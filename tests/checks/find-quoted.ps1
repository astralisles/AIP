# find: several words given as ONE quoted argument are still separate words (T-0974: every model in the pilot did this).
$out = (& $aip find examples\hello "split text" | Out-String).Trim()
Report 'find: a quoted "split text" finds textSplit' ($LASTEXITCODE -eq 0 -and $out.Contains('textSplit(')) $out
$out = (& $aip find examples\hello zzzqqq | Out-String).Trim()
Report 'find: no match prints a fix line' ($out.Contains('no function matches: zzzqqq') -and $out.Contains('fix:')) $out
$spec = (& $aip spec | Out-String)
Report 'spec: says how to loop and names std functions' ($spec.Contains('LOOPS') -and $spec.Contains('printLine') -and $spec.Contains('noop()')) ''
