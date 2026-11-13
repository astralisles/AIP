# Building and testing

- Run everything: `scripts\test.ps1` (prints one PASS/FAIL line per test, ends with `ALL PASSED`; exit code = failures).
- One program: `scripts\aip.ps1 run examples\<name>` (also `check`, `index`, `build [--target linux-x64]`).
  `aip.ps1` rebuilds the compiler only when its sources changed; use it rather than calling `dotnet` yourself.
- Tests are discovered from files; `scripts\test.ps1` should rarely change. See `tests/checks/README.md`.
  - `examples/<name>/expected-output.txt`: exact output of `aip run`, one line per printed line.
  - `tests/errors/<case>/expected.txt`: text the compiler must print when it rejects the project.
  - `tests/checks/<feature>.ps1`: anything else (built programs, stderr, env variables, files written).
- A new feature ships with a test in one of these forms, and an example under `examples/` if users can run it.
