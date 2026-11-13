# 100000 one-at-a-time updates on every collection must finish quickly (O(log n) each).
# The old copy-everything collections needed minutes for this. Output is checked by examples/scaling.
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$out = AipOut run examples\scaling
$sw.Stop()
$secs = [math]::Round($sw.Elapsed.TotalSeconds, 1)
Report 'scaling-time' (($out -match 'set 100000') -and $sw.Elapsed.TotalSeconds -lt 90) "100k adds per collection took $secs s (limit 90 s)"
