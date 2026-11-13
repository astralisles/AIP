# ints: overflow of a sized integer stops the program with a clear run-time error (never wraps silently).
$out = AipBoth run examples\intoverflow
Report 'ints: I8 overflow is a run-time error' ($out -match 'OverflowException' -and $out -match 'before') $out

# bits: a shift count outside 0..width-1 is a run-time error, not a silent mask.
$out = AipBoth run examples\bitoverflow
Report 'bits: shift count 8 on U8 is a run-time error' ($out -match 'shift count 8' -and $out -match 'before') $out
