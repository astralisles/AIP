# Profiling and inspection for an LLM (T-0938)

Answer: yes, a machine-readable mode, but as plain environment variables on the built program, not a separate
build or a debugger. An LLM cannot drive an interactive debugger well; it can read a JSON file. Because values are
immutable and there are no globals (T-0925), "the state" of a run is just the arguments of each call, so a call log
answers "what was the state when X happened" without breakpoints.

- `AIP_PROFILE=<file>` (or `-` for stderr): at exit writes one JSON object: `exitCode`, `error` (the runtime error, if any),
  `wallMs`, `cpuMs`, `peakWorkingSetBytes`, `allocatedBytes`, `gcCollections` (gen 0/1/2), `maxCallDepth`, and `functions`:
  per AIP function `calls` and `selfMs` (time excluding callees), slowest first. Also written when the program crashes.
- `AIP_CALLS=<file>` (or `-`): one JSON line per function call, in call order: `n`, `depth`, `fn`, `args` (as text, cut at 120 chars).
  Capped by `AIP_CALLS_MAX` (default 10000); a final `{"truncated":true,...}` line says how many calls were not logged.
- Both off by default: the cost is one bool check per call. They work with `aip run` and with a built program, no rebuild needed.
  Example: `$env:AIP_PROFILE="p.json"; scripts\aip.ps1 run examples\adding`.
- Existing aids stay: `--verbose` (compiler steps), `logLine` (program messages), `AIP_TRACE=1` (stack of a crash).

Limits and next steps: List/Map/Struct arguments print as their C# type name, not their contents (add a `Rt.Show`
that prints values); return values are not logged; the stats are for one thread, so tasks (T-0935) need per-task
tables merged at `taskAwait`; a sampling mode (per-line, not per-function) and `ensures` failures as structured
errors are not done. `index.txt` (function list with effects) plus `profile.json` is the intended pair of files to give an LLM.
