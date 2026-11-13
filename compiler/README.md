# aip compiler (minimal)

Front end (`Parser.cs`, `Checker.cs`) is target independent. A back end implements
`IBackend` (`Model.cs`) and turns the checked program into a runnable program.
The only back end so far is `CsBackend.cs`: it emits C# and builds it with the .NET SDK.

    scripts\aip.ps1 run examples\hello
    scripts\aip.ps1 check examples\hello
    scripts\aip.ps1 index examples\hello
    scripts\aip.ps1 build examples\hello [--target linux-x64]
    scripts\test.ps1

Entry point: a function named `main`, no parameters, returns Unit. Its `effects`
line lists everything the program can do. Every function must be reachable from `main`.

Standard library: `std/*.fn` are header-only `extern` functions; each back end
supplies the implementation (see `Externs` in `CsBackend.cs`).

Supported so far: `.fn` files; Int, Text, Bool, Unit; `let`, calls, operators,
`if/then/else`, text with `{expr}` parts. Ranges, `requires`, `ensures` and
`example` are parsed and type-checked but not yet proved or run.

## Asking questions (for an LLM or a person)

`aip query <project> <verb> [arg]` answers from the checked program: `list`, `fn NAME`
(signature, purpose, header, file:line, calls, called by), `find WORDS` (names and purposes),
`callers NAME`, `calls NAME`, `effect NAME` (`terminal`, `terminal.write`, `pure`).
Output is plain lines, one function per line, errors as `error code: message`.

When is the index updated? On every query, so it is never stale: each run re-parses and re-checks
the whole program. To make that fast on big projects, file reading and checking run on all cores, and
the text of every source file is kept in `build/sources.cache` (one file open instead of one per
function). Each run compares every file's size and last-write time with the cache and re-reads any
file that changed, was added or was deleted; a missing or damaged cache is rebuilt. Safe to delete.
`aip build` still writes `build/index.txt` as a snapshot. `AIP_TIMINGS=1` prints where the time goes.

Measured (`python scripts/bench-query.py <dir>`, generated projects, query after one file was edited):
1,000 functions 0.19 s, 5,000 0.29 s, 20,000 0.46 s, 50,000 1.1 s, 200,000 3.7 s.
Next step if engine-sized projects need faster answers: a long-running `aip serve` that keeps the
checked program in memory and re-checks only changed files.
