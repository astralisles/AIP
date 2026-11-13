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

Standard library: `std/<group>/*.fn` are header-only `extern` functions, grouped by type or effect
(`text`, `list`, `map`, `set`, `file`, `terminal`, `log`, `process`, `core`; see `std/README.md`).
`scripts\aip.ps1 std` lists them. Each back end supplies the implementation (see `Externs` in `CsBackend.cs`).

Supported so far: `.fn` files; Int, Text, Bool, Unit; `List<T>`, `Set<T>`, `Map<K,V>` (immutable: every change returns a new value; see `examples/collections`); `let`, calls, operators,
`if/then/else`, text with `{expr}` parts; `.struct` and `.variant` files (constructors `Point(1, 2)`,
`.field` reads) and statement-level `match` (`let x = match v` or a Unit `match v`, one `Case(a, b) => expr` arm
per line, every case required). See `examples/shapes`. Ranges, `requires`, `ensures` and
`example` are parsed and type-checked but not yet proved or run.

<<<<<<< HEAD
Control flow: conditionals are `if c then a else b` (chainable with `else if`, one line, an expression; with Unit
branches it is a statement) and `match`. There is no `while`/`for`: a loop is a function that calls itself with the
next state (accumulator parameters), so state stays immutable. One million iterations run fine (512 MB stack). See
`examples/control`. Multi-line `if` blocks and `Fn`-based `listMap`/`listFold` are not done (see T-0928).

## Road to bootstrapping (compiler written in AIP)
=======
## Design decisions, features and roadmap
>>>>>>> origin/master

One file per topic in `docs/design/` (for example `security-T-0936.md`, `bootstrapping.md`).
Logging and debugging: `docs/design/logging-T-0934.md`; profiling: `docs/design/profiling-T-0938.md`.
A new decision is a new file there; do not append it to this README.
