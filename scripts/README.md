# scripts

- `render-files-to-frames.ps1` — renders text files as numbered PNG frames (one file per step) for an evidence GIF. Off-screen, opens no window.
  `powershell -File scripts\render-files-to-frames.ps1 -Files mock\src\Player.struct,mock\src\applyDamage.fn -OutDir <evidence>\frames\<name> -Title "Hello files"`

- `aip.ps1` - builds the compiler only when its sources changed (skips the 2.5 s no-op `dotnet build`), then runs it. Program builds are cached by a hash of the generated C# (identical code is not re-published) and runs it: `scripts\aip.ps1 run examples\hello`, or `build examples\hello [--target linux-x64]`. How to run locally: see the root `README`.
- `test.ps1` - test harness; finds tests from files: `examples\*\expected-output.txt`, `tests\errors\*\expected.txt`, `tests\checks\*.ps1` (see `tests\checks\README.md`). Add a test by adding a file, not by editing this script.
- `aip.ps1` also rebuilds when std files were moved, added or deleted (same timestamps), and the build removes stale std copies from its output.
- `capture-session.ps1` - runs commands and writes `> command` plus real output to a log (evidence).
- `render-log-to-frames.ps1` - renders such a log as PNG frames revealed line by line (for a GIF).
