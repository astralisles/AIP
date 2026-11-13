# aip compiler (minimal)

Front end (`Parser.cs`, `Checker.cs`) is target independent. A back end implements
`IBackend` (`Model.cs`) and turns the checked program into a runnable program.
The only back end so far is `CsBackend.cs`: it emits C# and builds it with the .NET SDK.

    scripts\aip.ps1 run examples\hello
    scripts\aip.ps1 check examples\hello
    scripts\aip.ps1 index examples\hello
    scripts\aip.ps1 find examples\shapes area      search project + std functions
    scripts\aip.ps1 spec                           one-page language spec for a model
    scripts\aip.ps1 build examples\hello [--target linux-x64]
    scripts\test.ps1

Entry point: a function named `main`, no parameters, returns Unit. Its `effects`
line lists everything the program can do. Every function must be reachable from `main`.

Standard library: `std/<group>/*.fn` are header-only `extern` functions, grouped by type or effect
(`text`, `list`, `map`, `set`, `file`, `terminal`, `log`, `process`, `core`; see `std/README.md`).
`scripts\aip.ps1 std` lists them. Each back end supplies the implementation (see `CsBackend.Externs.*.cs`).

Supported so far: `.fn` files; Int (64-bit signed), I8 I16 I32 U8 U16 U32 U64, F32 F64 (floats), bit operations `bitAnd`.. (exact size, see `docs/design/data-types-T-0945.md` and `examples/ints`, `examples/floats`, `examples/bits`), Text, Bool, Unit; `List<T>`, `Set<T>`, `Map<K,V>` (immutable: every change returns a new value; see `examples/collections`); `let`, calls, operators,
`if/then/else`, text with `{expr}` parts; `.struct` and `.variant` files (constructors `Point(1, 2)`,
`.field` reads; generic ones such as `variant Option<T>`, and generic functions such as `fn firstOf(items List<T>) -> Option<T>`, see `examples/generics`) and statement-level `match` (`let x = match v` or a Unit `match v`, one `Case(a, b) => expr` arm
per line, every case required). See `examples/shapes`. `.const` files (`docs/design/consts-T-0941.md`, `examples/consts`). Ranges, `requires`, `ensures` and
`example` are type-checked, and `aip test` runs them (see below); they are not proved.

## Static analysis and consistency checks (T-0933)

    scripts\aip.ps1 lint <project>   warnings: unused-param, self-comparison, constant-condition,
                                     division-by-zero, literal-out-of-range, requires-always-false, unbounded-recursion (Lint.cs)
    scripts\aip.ps1 test <project>   runs every `example` line of pure functions in an interpreter
                                     (Interp.cs), checking requires / ensures / Int ranges on every call
    scripts\aip.ps1 fuzz <project>   damages each source file many ways; the compiler must report
                                     errors, never crash (Fuzz.cs)
    scripts\aip.ps1 emit <project>   writes the generated C# and index without building

Gates in `scripts\` (all run by `scripts\test.ps1`):
`check-error-coverage.ps1` (every error code and lint warning has a test case, and no test names a
removed code), `check-externs.ps1` (std headers, C# templates and interpreter agree; `pure` is never a lie),
`check-index.ps1` (the index matches the headers, rebuilt independently of the compiler),
`check-determinism.ps1` (two `emit` runs give byte-identical output).
Test cases: `tests\errors` (`aip check` must reject), `tests\lint` (`aip lint` must warn),
`tests\contracts` (`aip test` must fail); each has an `expected.txt` with the message.

## Asking questions (for an LLM or a person)

`aip query <project> <verb> [arg]` answers from the checked program: `list`, `fn NAME`
(signature, purpose, header, file:line, calls, called by), `find WORDS` (names and purposes),
`callers NAME`, `calls NAME`, `effect NAME` (`terminal`, `terminal.write`, `pure`),
`type NAME` (fields or cases, and who uses it), `users TYPE` (functions that take, return, construct, match or
read a field of a struct/variant, and types with a field of it).
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

Control flow: conditionals are `if c then a else b` (chainable with `else if`, one line, an expression; with Unit
branches it is a statement), a multi-line `if c then` / `else` block of Unit statements, and `match`. Loops are recursion (no `while`/`for`); see `docs/design/control-flow-T-0943.md`
and `examples/control`.

## Design decisions, features and roadmap

Open questions and what was decided (`aip spec`, `aip find`, Result/Option, AI-first rule): `docs/design/open-questions-T-0944.md`.
One file per topic in `docs/design/` (for example `security-T-0936.md`, `bootstrapping.md`).
Contracts and static verification (what is checked, what could be proved): `docs/design/contracts-T-0950.md`.
Function restraints (`cannot-fail`, `fails`, `decreases`): `docs/design/function-restraints-T-0966.md`.
File types and the parts of each file: `docs/design/file-anatomy-T-0948.md`. Constants: `docs/design/consts-T-0941.md`.
Logging and debugging: `docs/design/logging-T-0934.md`; profiling: `docs/design/profiling-T-0938.md`.
Native shared libraries (`native "lib" "symbol"`, dll/so/dylib): `docs/design/native-libraries-T-0956.md`.
A new decision is a new file there; do not append it to this README.
