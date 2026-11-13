# logging and debugging: logLine goes to stderr only; --verbose logs compiler steps; crashes are reported cleanly.
$stdout = AipOut run examples\logging
Report 'logging: stdout has only the normal output' ($stdout -eq 'Hello from the logging example') "got: $stdout"
$all = AipBoth run examples\logging --verbose
Report 'logging: logLine and --verbose go to stderr' ($all.Contains('debug: starting') -and $all.Contains('[aip') -and $all.Contains('checked: 0 error(s)')) $all
$crash = AipBoth run examples\crash
Report 'crash: run-time error is reported with exit code 1' ($LASTEXITCODE -eq 1 -and $crash.Contains('runtime error: KeyNotFoundException') -and -not $crash.Contains(' at Fn.')) $crash
$env:AIP_TRACE = '1'
try { $crash = AipBoth run examples\crash } finally { Remove-Item Env:\AIP_TRACE }
Report 'crash: AIP_TRACE=1 prints the stack trace' ($crash.Contains('at Fn.main')) $crash
