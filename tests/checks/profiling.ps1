# profiling: AIP_PROFILE writes JSON (time, memory, per-function calls); AIP_CALLS writes one JSON line per call with arguments.
$tmp = NewTempDir 'aip-prof-'
$env:AIP_PROFILE = Join-Path $tmp 'profile.json'; $env:AIP_CALLS = Join-Path $tmp 'calls.jsonl'; $env:AIP_CALLS_MAX = '1'
try { $null = AipBoth run examples\adding } finally { Remove-Item Env:\AIP_PROFILE, Env:\AIP_CALLS, Env:\AIP_CALLS_MAX }
$p = try { Get-Content (Join-Path $tmp 'profile.json') -Raw | ConvertFrom-Json } catch { $null }
$c = @(try { Get-Content (Join-Path $tmp 'calls.jsonl') } catch { @() })
Report 'profile: AIP_PROFILE and AIP_CALLS write valid JSON' ($p -and $p.schema -eq 1 -and $p.functions.Count -gt 0 -and $p.maxCallDepth -ge 1 -and $c.Count -eq 2 -and $c[1].Contains('truncated')) "profile: $p calls: $c"
Remove-Item -Recurse -Force $tmp
