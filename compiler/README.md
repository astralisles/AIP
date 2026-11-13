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

Supported so far: `.fn` files; Int, Text, Bool, Unit; `List<T>`, `Set<T>`, `Map<K,V>` (immutable: every change returns a new value; see `examples/collections`); `let`, calls, operators,
`if/then/else`, text with `{expr}` parts; `.struct` and `.variant` files (constructors `Point(1, 2)`,
`.field` reads) and statement-level `match` (`let x = match v` or a Unit `match v`, one `Case(a, b) => expr` arm
per line, every case required). See `examples/shapes`. Ranges, `requires`, `ensures` and
`example` are type-checked, and `aip test` runs them (see below); they are not proved.

## Static analysis and consistency checks (T-0933)

    scripts\aip.ps1 lint <project>   warnings: unused-param, self-comparison, constant-condition,
                                     division-by-zero, literal-out-of-range, unbounded-recursion (Lint.cs)
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

## Road to bootstrapping (compiler written in AIP)

Done: `struct`, `variant`, `match`; `List<T>` (type-erased at run time), text helpers (`textLength`, `textSlice`, `textCode`, ...), file and
argument access, Unit `if` statements, deep recursion (512 MB stack). `examples/lexer` is the first compiler stage
written in AIP; `scripts\compare-lexers.ps1` checks it prints the same tokens as the C# lexer (`aip tokens <file>`)
for every `.fn` file, including its own source. It is part of `scripts\test.ps1`.

Still missing before the compiler can compile itself:
1. `.const` files (`.struct`/`.variant`/`match` are done; a match is only allowed as a whole statement or `let` value, not nested in other expressions).
2. Parser in AIP (lines to AST), then checker, then an emitter that writes the C# text.
3. (`Map` and `Set` now exist.) A way to run `dotnet publish` (a `process.run` effect) from AIP.
4. Then: C# compiler builds the AIP compiler (stage 1), stage 1 builds itself (stage 2), stage 2 output must equal stage 1.

## Design decision: global state (T-0925)

No global mutable state. There are no global variables; all values are immutable and
a function sees only its parameters and constants. Reasons: each function must be
understandable from its file and signature alone (the language's core promise), results
must be reproducible for `example`/`ensures` checking, and the bootstrapped compiler
must be deterministic (stage 2 output == stage 1 output).

Allowed: immutable `.const` files (compile-time constants, no effect needed).

Needed state goes through an explicit, effect-tracked handle, not a hidden global: a
program that wants a counter, cache or config declares an effect (e.g. `state.read` /
`state.write`) and passes the handle/value as a parameter, so it shows in the index.
Not implemented yet; revisit only if the compiler-in-AIP proves too clumsy without it.

## Design decision: function pointers (T-0928)

Yes, but as a restricted feature, and not yet. Higher-order operations (`map`, `filter`,
`fold`, sort by key, callbacks in a compiler pass) are needed for the bootstrapped
compiler; without them every loop is hand-written recursion.

Shape: a function type `Fn<(A, B), R>` (effects part of the type, e.g. `Fn<(Int), Int> !file.read`).
A value of that type can only be a reference to a named top-level function (`&name`). That
keeps the promises of the language:
- No closures, so no captured hidden state (consistent with the no-global-state decision).
- No anonymous lambdas: every callee has a file, a signature and an index line.
- A function taking a `Fn` parameter may only perform the effects its own `effects` line
  lists plus those in the `Fn` type, so effects stay visible in the index.
- Reachability: `&name` counts as a call from the referencing function.
- Back end: C# delegate (`Func<..>`); a back end without delegates may use a lookup table.

Not implemented. Until then, use recursion over `List<T>`. Build it after `struct`/`variant`/`match`,
then add std `listMap`, `listFilter`, `listFold`.
