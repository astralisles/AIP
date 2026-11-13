# tests/checks

One `.ps1` file per feature. `scripts\test.ps1` dot-sources every file here (in name order), so a new
check is a new file, never an edit to `test.ps1`. A check file can use:

- `Report <name> <ok> <detail>` — prints PASS/FAIL and counts failures.
- `$aip` (path to `scripts\aip.ps1`), `$root` (repo root; the working folder is reset to it after each file).
- `AipOut <args>` (stdout only, trimmed) and `AipBoth <args>` (stdout + stderr) — run aip in its own process.
- `NewTempDir <prefix>` — a fresh folder under %TEMP%; delete it at the end of the check.

Simpler cases need no script:
- Program output: put `expected-output.txt` (one line per printed line) in `examples\<name>\`.
- Compile errors: add `tests\errors\<case>\` with the project and an `expected.txt`.
