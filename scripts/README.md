# scripts

- `render-files-to-frames.ps1` — renders text files as numbered PNG frames (one file per step) for an evidence GIF. Off-screen, opens no window.
  `powershell -File scripts\render-files-to-frames.ps1 -Files mock\src\Player.struct,mock\src\applyDamage.fn -OutDir <evidence>\frames\<name> -Title "Hello files"`

- `aip.ps1` - builds the compiler only when its sources changed (skips the 2.5 s no-op `dotnet build`), then runs it. Program builds are cached by a hash of the generated C# (identical code is not re-published) and runs it: `scripts\aip.ps1 run examples\hello`, or `build examples\hello [--target linux-x64]`. How to run locally: see the root `README`.
- `test.ps1` - builds and runs the examples, checks every project in `tests\errors` is rejected (and `tests\lint` warns, `tests\contracts` fails `aip test`) with the text in its `expected.txt`, runs `aip lint` / `aip test` / `aip fuzz` on every example, then the four gates below. About 75 s.
- `check-error-coverage.ps1` - every compiler error code and lint warning has a test case, and no test names a removed one. `-List` prints all codes.
- `check-externs.ps1` - `compiler\std` headers, the C# templates and the `aip test` interpreter agree, and `pure` std functions really touch nothing.
- `check-index.ps1 [dirs]` - the index matches the function headers (derived independently of the compiler), and `build\index.txt` is not stale.
- `check-determinism.ps1 [dirs]` - two `aip emit` runs give byte-identical files.
- `aip.ps1` also rebuilds when std files were moved, added or deleted (same timestamps), and the build removes stale std copies from its output.
- `capture-session.ps1` - runs commands and writes `> command` plus real output to a log (evidence).
- `render-log-to-frames.ps1` - renders such a log as PNG frames revealed line by line (for a GIF).
