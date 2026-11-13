# aip compiler (minimal)

Front end (`Parser.cs`, `Checker.cs`) is target independent. A back end implements
`IBackend` (`Model.cs`) and turns the checked program into a runnable program.
The only back end so far is `CsBackend.cs`: it emits C# and builds it with the .NET SDK.

    scripts\aip.ps1 run examples\hello
    scripts\aip.ps1 check examples\hello
    scripts\aip.ps1 index examples\hello
    scripts\aip.ps1 build examples\hello [--target linux-x64]
    scripts\test.ps1

## Logging and debugging

- `--verbose` / `-v` (or env `AIP_VERBOSE=1`) on any `aip` command logs steps with timings to stderr: files loaded, check result, whether `dotnet publish` ran or was skipped, program exit code. stdout stays unchanged.
- `logLine(text)` (std, effect `log.write`) writes a diagnostic line to stderr, so it never mixes with program output. See `examples/logging`.
- A crashing program prints `runtime error: <type>: <message>` to stderr and exits with code 1. Set `AIP_TRACE=1` to also print the stack (generated C# frames `Fn.<function>`). See `examples/crash`.

## Profiling and inspection for an LLM (T-0938)

Answer: yes, a machine-readable mode, but as plain environment variables on the built program, not a separate
build or a debugger. An LLM cannot drive an interactive debugger well; it can read a JSON file. Because values are
immutable and there are no globals (T-0925), "the state" of a run is just the arguments of each call, so a call log
answers "what was the state when X happened" without breakpoints.

- `AIP_PROFILE=<file>` (or `-` for stderr): at exit writes one JSON object: `exitCode`, `error` (the runtime error, if any),
  `wallMs`, `cpuMs`, `peakWorkingSetBytes`, `allocatedBytes`, `gcCollections` (gen 0/1/2), `maxCallDepth`, and `functions`:
  per AIP function `calls` and `selfMs` (time excluding callees), slowest first. Also written when the program crashes.
- `AIP_CALLS=<file>` (or `-`): one JSON line per function call, in call order: `n`, `depth`, `fn`, `args` (as text, cut at 120 chars).
  Capped by `AIP_CALLS_MAX` (default 10000); a final `{"truncated":true,...}` line says how many calls were not logged.
- Both off by default: the cost is one bool check per call. They work with `aip run` and with a built program, no rebuild needed.
  Example: `$env:AIP_PROFILE="p.json"; scripts\aip.ps1 run examples\adding`.
- Existing aids stay: `--verbose` (compiler steps), `logLine` (program messages), `AIP_TRACE=1` (stack of a crash).

Limits and next steps: List/Map/Struct arguments print as their C# type name, not their contents (add a `Rt.Show`
that prints values); return values are not logged; the stats are for one thread, so tasks (T-0935) need per-task
tables merged at `taskAwait`; a sampling mode (per-line, not per-function) and `ensures` failures as structured
errors are not done. `index.txt` (function list with effects) plus `profile.json` is the intended pair of files to give an LLM.

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
- Shared-state bugs (races, hidden mutation): immutable values, no globals (T-0925), no threads today; planned parallelism (T-0935) shares nothing and keeps results deterministic.
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
Never add `unsafe`, FFI, reflection, `eval`, or a text-to-code path. Mutation stays out. Tasks (T-0935) must keep the same limits per task (stack size, path scope, memory).

**Not covered by the language**: bugs in the .NET runtime, the OS, or a back end. A future native back end (no .NET)
must re-prove each bullet above (bounds checks, overflow checks) in its own runtime; the checks belong in the shared
spec of `std`, not only in `CsBackend.cs`.

## Design decision: parallelism (T-0935)

Yes, but only as deterministic, effect-visible parallelism, and not yet. Because values are
immutable and there is no global state (T-0925), pure functions cannot race, so parallelism
needs no locks, atomics or shared memory in the language.

Shape (builds on `Fn` from T-0928):
- Data parallelism first: std `listMapPar(list, &f)`, `listFoldPar(list, &f, &combine)`.
  `f` must be pure (no effects in its `Fn` type); `combine` must be associative. The result
  is identical to the sequential `listMap` / `listFold`, always in input order, so
  `example`/`ensures` checking and "stage 2 output == stage 1 output" still hold.
  The back end chooses the thread count; it is never visible to the program.
- Parallel effects (e.g. compile many files, each reading and writing files): `taskRun(&f, arg)`
  returns a `Task<R>`; `taskAwait(t)` returns the result. The effects of `f` must appear in the
  caller's `effects` line plus a `parallel` effect, so the index shows which functions fork.
  Tasks share nothing: arguments are copied/immutable and the only way back is `taskAwait`.
  Results are consumed in a program-chosen order, so output order stays deterministic.
- No shared mutable state, no locks, no channels, no `async`/`await` coloring. Any
  cross-task communication needs the explicit state handle from T-0925, not planned now.
- Errors: a failing task surfaces at its `taskAwait`; unawaited tasks are a compile error.
- Back end: C# `Parallel`/`Task` (the main thread already runs on a 512 MB stack thread;
  task threads need the same stack size for deep recursion); other back ends may run sequentially,
  which must give the same result.

Not implemented. Needs `Fn` first. Immediate use: compiling the files of a project in the
bootstrapped compiler (parse each `.fn` in parallel, check and emit in order).

## Open questions we are not asking yet (T-0944)

Decided so far: global state, function pointers, security, parallelism, profiling, std layout. These are the gaps
left, ordered by how much they would hurt if answered late. Each is a question, not a decision.

1. **Does an LLM actually write AIP better than it writes Python?** Everything so far is argued, nothing is measured.
   Build a small benchmark: 20 tasks, the same model writes each in AIP and in a baseline language, score first-try
   compile rate, test pass rate, tokens used, and edit success. Without it we cannot tell a good rule from a rule that
   only feels tidy. (`examples/` plus `tests/errors` are a starting corpus.)
2. **Is the compiler error message good enough for a model to fix its own mistake?** Errors are the LLM's main
   feedback loop. Check: one stable error code, the file and line, the offending text, and a suggested fix, in a
   machine-readable form (`--json`). Measure how often a model fixes the error in one retry.
3. **How does an LLM learn the language it was never trained on?** Needs a short spec (one page) that fits in a
   prompt, and an `aip` command that prints it (`aip spec`). Decide how big it may get; if the spec outgrows the
   context budget, the one-function-per-file idea is paying for itself less than we think.
4. **What happens when the index grows?** One line per function is fine for 50 functions, not for 50,000.
   Decide: grouping by folder, search (`aip find "parse json"`), or a per-folder summary line.
5. **How do errors and absence work?** Not yet decided: no exceptions, `Option`/`Result` values, or crash only.
   Today a runtime error just crashes. A language for LLMs probably wants `Result<T,E>` in the signature so the
   failure modes show in the index like effects do. This also decides what `ensures` failures become.
6. **How do people (and models) depend on other people's code?** "No imports" means no libraries. Decide how code is
   shared: copy a folder in, a versioned package, or only std. Needs an answer for supply-chain trust (see T-0936).
7. **How do we change the language without breaking old programs?** A version line in each project, and a rule for
   what may change. Cheap now, painful after the first outside user.
8. **What is the testing story?** `example` lines are parsed but never run. Running them is the cheapest big win: they
   are tests that live next to the function and show an LLM how the function is used. Also decide on property tests.
9. **Which numbers?** Only `Int` exists. Overflow (wrap, crash or big), `Float`, decimals for money, `Bytes`,
   and Unicode text (code points vs bytes; `textSlice` today) all need an answer before real programs.
10. **How does a program talk to the outside world?** Network, time, randomness, environment and processes are
    effects too. Each needs a name, a scope (see T-0936 path scope) and a deterministic replay so tests stay stable.
11. **Is the bootstrap goal still the right goal?** Self-hosting proves the language is capable, but it costs time that
    could go to questions 1, 2 and 8. Decide whether self-hosting is a milestone or the project.
12. **Who is the user: the model or the human who reads its output?** Both read the code. Decide which wins when
    they conflict (e.g. terse names vs readable names), and whether there is an `aip explain <function>` for humans.
13. **What does "done" mean for the prototype?** Without a success test (for example: a model builds a 500-line tool
    from the spec alone, with no human edits) we cannot tell progress from activity.

Recommended next three, in order: 1 (benchmark, it decides whether the rest matters), 8 (run `example` lines),
5 (errors as values). Nothing here is implemented; this section is documentation only.
