# AIP — instructions for Claude

AIP is a programming language designed for AI: one function per file, effects in the signature,
a generated one-line-per-function index. Compiler in C# (`compiler/`), runnable programs in `examples/`.

Several agents work on this repo in parallel branches. Keep this file short and stable: put
guidance in one topic file per subject under `docs/claude/` and import it here (one line each,
keep the list sorted). `.gitattributes` merges this file with `merge=union`, so two branches that each add an
import line merge cleanly; do not put prose here that other branches are likely to edit.

@docs/claude/compiler.md
@docs/claude/conflicts.md
@docs/claude/docs.md
@docs/claude/testing.md
