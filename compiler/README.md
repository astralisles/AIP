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
`example` are parsed and type-checked but not yet proved or run.

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

## Design decision: security and memory safety (T-0936)

Honest answer: no language is "immune". We can make whole bug classes impossible by
construction, and keep the rest small, visible and checkable. Today's state, by class:

**Already closed by construction (C# back end, verified in the code)**
- Memory corruption (buffer overflow, use-after-free, uninitialised reads): the language has no
  pointers, no manual memory, no `unsafe`, no FFI, no `DllImport`. Generated code is managed C#;
  `listGet`, `textCode` and `mapGet` throw on a bad index/key instead of reading past the end.
- Integer overflow: the generated project sets `CheckForOverflowUnderflow`, so `Int` (64-bit) arithmetic
  throws instead of wrapping. `textSlice` clamps.
- Eval/RCE from data: there is no `eval`, no reflection, no dynamic code loading, no string-to-code path.
  Programs cannot start processes or open sockets: the only effects are `terminal.write`, `file.read`, `file.write`.
- Shared-state bugs (races, hidden mutation): immutable values, no globals (T-0925), no threads in the language.
- Injection into the generated C#: identifiers are ASCII letters/digits/`_` only (lexer) and emitted with `@`;
  text literals are escaped by `Quote` (every non-printable becomes `\uXXXX`); doc text is XML-escaped and
  cannot contain a line break (a lone CR was tried: it splits the line, and the file is rejected as `missing-doc`).

**Open gaps (to do, in this order)**
1. Effects are only declared, not enforced at run time. `file.read`/`file.write` accept any path, so a program
   (or a compiler pass written in AIP that handles untrusted `.fn` files) can read or overwrite anything the user can
   (`../..`, absolute paths). Fix: path-scoped effects, e.g. `file.read` limited to a root passed in by the host
   (`--allow-read <dir>`, `--allow-write <dir>`), checked in `Rt` after `Path.GetFullPath` (and reject symlink escape).
   The same rule applies to the future `process.run`: an allow-list of programs, arguments passed as an array, never one shell string.
2. Resource exhaustion (DoS, not corruption): unbounded recursion overflows the 512 MB stack and kills the process;
   huge `Text`/`List` allocations exhaust memory; `textToInt` and `fileRead` on hostile input throw (a crash, which is the safe outcome).
   Fix: a recursion-depth counter in generated code, optional `--max-memory`, and run untrusted programs under an OS limit.
3. The compiler itself reads untrusted source: keep it free of `Process`/reflection use on source-derived strings
   (today only `dotnet` with fixed arguments), cap file size and nesting depth in `Parser.cs` so a hostile `.fn` cannot
   overflow the compiler's own stack.
4. Supply chain: the build runs `dotnet` and pulls the SDK's NuGet restore. Pin the SDK (`global.json`), generate the
   csproj with no package references (true today), and restore offline once dependencies are frozen.
5. Tests: add `tests/errors` and run-time cases that must stay true: path escape rejected, overflow throws, bad index throws,
   hostile text literal does not change the generated code. Run a fuzz pass (random bytes into the lexer/parser) in `scripts\test.ps1`.

**Rule for new features** (including `Fn` pointers and `state` handles): a feature that can reach outside the program
(files, processes, network, time, randomness) must be an effect named in the signature, scoped by the host, and shown in the index.
Never add `unsafe`, FFI, reflection, `eval`, or a text-to-code path. Mutation stays out.

**Not covered by the language**: bugs in the .NET runtime, the OS, or a back end. A future native back end (no .NET)
must re-prove each bullet above (bounds checks, overflow checks) in its own runtime; the checks belong in the shared
spec of `std`, not only in `CsBackend.cs`.
