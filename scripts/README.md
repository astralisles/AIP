# scripts

- `aip.ps1` - builds the compiler only when its sources changed (skips the 2.5 s no-op `dotnet build`), then runs it. Program builds are cached by a hash of the generated C# (identical code is not re-published): `scripts\aip.ps1 run examples\hello`, or `build examples\hello [--target linux-x64]`. How to run locally: see the root `README`. It also rebuilds when std files were moved, added or deleted (same timestamps), and removes stale std copies from its output.
- `bench-attempt.ps1` - "can a model write AIP" benchmark (`tests\bench`): runs `spec|find|check|run` for a model's project and logs every attempt; `-Summary <folder>` prints attempts and error codes per task.
- `bench-query.ps1` - times `aip query` on generated projects of 10 to 5000 functions.
- `bench-query.py` - same at game scale (default 1,000 to 50,000 functions): first check, query without cache, cached, and after one edit. `python scripts/bench-query.py <empty dir> [1000,50000] [out.txt]`; run from bash, not PowerShell.
- `bootstrap.ps1` - staged bootstrap test: the C# compiler builds `examples\aipc` (stage 1), stage 1 reads its own source like the C# compiler; stage 2 (aipc builds itself) is reported as SKIP until aipc can compile. Exit code = failures.
- `capture-session.ps1` - runs commands and writes `> command` plus real output to a log (evidence).
- `check-determinism.ps1 [dirs]` - two `aip emit` runs give byte-identical files.
- `check-error-coverage.ps1` - every compiler error code and lint warning has a test case, and no test names a removed one. `-List` prints all codes.
- `check-externs.ps1` - `compiler\std` headers, the C# templates and the `aip test` interpreter agree, and `pure` std functions really touch nothing.
- `check-index.ps1 [dirs]` - the index matches the function headers (derived independently of the compiler), and `build\index.txt` is not stale.
- `changed-tests.ps1 [-Base ref]` - reads the files git reports as changed (committed since the merge-base with main/master, uncommitted, untracked) and says which tests they need; docs-only changes need none, an edit under `examples\<name>`, `tests\errors\<case>` or `tests\checks\<file>.ps1` needs just that test, anything else (compiler, scripts) needs all. Run it via `scripts\test.ps1 -Changed [-Base ref]`.
- `compare-ast.ps1` - bootstrap check: the AIP body parser (`examples\aipc`, `aipc ast <file>`) prints the same dump as `aip ast` for every `.fn` file the C# parser accepts. Exit code = mismatches.
- `compare-index.ps1` - bootstrap check: the AIP parser (`examples\aipc`, `aipc index <files>`) reads every header in `examples\` and `compiler\std` like the C# compiler (`aip index`, `aip std`). Exit code = mismatches.
- `compare-lexers.ps1` - bootstrap check: the AIP lexer (`examples\aipc`, `aipc tokens <file>`) prints the same tokens as `aip tokens` for every `.fn`/`.const` file. Exit code = mismatches.
- `fix-hidden-failure.ps1 <dirs>` - adds the `fails` header line to every function `aip lint` reports as `hidden-failure`, repeating until clean (use after merging branches that added `fails` functions and callers separately).
- `render-files-to-frames.ps1` — renders text files as numbered PNG frames (one file per step) for an evidence GIF. Off-screen, opens no window.
  `powershell -File scripts\render-files-to-frames.ps1 -Files mock\src\Player.struct,mock\src\applyDamage.fn -OutDir <evidence>\frames\<name> -Title "Hello files"`
- `render-log-to-frames.ps1` - renders such a log as PNG frames revealed line by line (for a GIF).
- `test.ps1` - test harness; finds tests from files: `examples\*\expected-output.txt`, `tests\errors\*\expected.txt`, `tests\checks\*.ps1` (see `tests\checks\README.md`). Add a test by adding a file, not by editing this script. `tests\checks\static-analysis.ps1` runs `tests\lint`, `tests\contracts`, `aip lint` / `aip test` / `aip fuzz` on every example, and the four gates below.
