# scripts

- `render-files-to-frames.ps1` — renders text files as numbered PNG frames (one file per step) for an evidence GIF. Off-screen, opens no window.
  `powershell -File scripts\render-files-to-frames.ps1 -Files mock\src\Player.struct,mock\src\applyDamage.fn -OutDir <evidence>\frames\<name> -Title "Hello files"`

- `bench-query.ps1` - times `aip query` on generated projects of 10 to 5000 functions.
- `bench-query.py` - same at game scale (default 1,000 to 50,000 functions): first check, query without cache, cached, and after one edit. `python scripts/bench-query.py <empty dir> [1000,50000] [out.txt]`; run from bash, not PowerShell.
- `aip.ps1` - builds the compiler if needed and runs it (also `query`, see compiler/README.md): `scripts\aip.ps1 run examples\hello`, or `build examples\hello [--target linux-x64]`. How to run locally: see the root `README`.
- `test.ps1` - builds and runs `examples\hello` (also through `aip run`), and checks every project in `tests\errors` is rejected with the text in its `expected.txt`.
- `capture-session.ps1` - runs commands and writes `> command` plus real output to a log (evidence).
- `render-log-to-frames.ps1` - renders such a log as PNG frames revealed line by line (for a GIF).
