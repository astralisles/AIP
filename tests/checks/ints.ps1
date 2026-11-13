# ints: overflow of a sized integer stops the program with a clear run-time error (never wraps silently).
$out = AipBoth run examples\intoverflow
Report 'ints: I8 overflow is a run-time error' ($out -match 'OverflowException' -and $out -match 'before') $out
