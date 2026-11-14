# Design decision: security and memory safety (T-0936)

Honest answer: no language is "immune". We can make whole bug classes impossible by
construction, and keep the rest small, visible and checkable. Today's state, by class:

**Already closed by construction (C# back end, verified in the code)**
- Memory corruption (buffer overflow, use-after-free, uninitialised reads): the language has no
  pointers, no manual memory, no `unsafe`, no FFI, no `DllImport`. Generated code is managed C#;
  `listGet`, `textCode` and `mapGet` throw on a bad index/key instead of reading past the end.
- Integer overflow: the generated project sets `CheckForOverflowUnderflow`, so `I64` (64-bit) arithmetic
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
