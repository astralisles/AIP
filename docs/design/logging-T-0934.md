# Logging and debugging (T-0934)

- `--verbose` / `-v` (or env `AIP_VERBOSE=1`) on any `aip` command logs steps with timings to stderr: files loaded, check result, whether `dotnet publish` ran or was skipped, program exit code. stdout stays unchanged.
- `logLine(text)` (std, effect `log.write`) writes a diagnostic line to stderr, so it never mixes with program output. See `examples/logging`.
- A crashing program prints `runtime error: <type>: <message>` to stderr and exits with code 1. Set `AIP_TRACE=1` to also print the stack (generated C# frames `Fn.<function>`). See `examples/crash`.
