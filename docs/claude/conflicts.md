# Avoiding merge conflicts between parallel branches

Many tickets run at once, each on its own branch, and are merged later. A conflict happens when two
branches edit the same lines, usually the end of a shared file. Prefer adding a new file over editing a shared one:

| You are adding | Do this | Not this |
|---|---|---|
| A design decision, answer or roadmap note | New file `docs/design/<topic>-T-<ticket>.md` | Appending a section to `compiler/README.md` |
| A test of program output | `examples/<name>/expected-output.txt` | Editing `scripts/test.ps1` |
| A compile-error test | `tests/errors/<case>/` with `expected.txt` | Editing `scripts/test.ps1` |
| Any other test | New `tests/checks/<feature>.ps1` | Editing `scripts/test.ps1` |
| Guidance for Claude | New `docs/claude/<topic>.md` plus one sorted `@` line in `CLAUDE.md` | Growing `CLAUDE.md` |
| A std function | New file `compiler/std/<name>.fn` (one function per file) | |

When you must edit a shared list (for example `scripts/README.md`, the root `README`), insert your line
in sorted position rather than at the end, and do not rewrap or reorder lines you are not changing.
Do not reformat whole files (line endings, indentation): it turns a small change into a whole-file conflict.
