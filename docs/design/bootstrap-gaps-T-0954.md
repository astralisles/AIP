# What else is needed to bootstrap the compiler (T-0954)

Status today: `examples/aipc` has the lexer and the header parser, both checked against the C# compiler on every test run
(`docs/design/bootstrapping.md`). The C# compiler is about 2,700 lines; the part that must be rewritten to compile itself is
`Parser.cs` (592), `Checker.cs` (546), `CsBackend.cs` (469), `Project.cs` (102) and `Program.cs` (181). `Lint`, `Interp`, `Query`,
`Fuzz` and `SourceCache` are not needed to bootstrap and stay in C# (or follow later).

Rule used below: the AIP compiler may only use features AIP has, so every gap is either a language or std feature the
compiler source needs, or a compiler stage still to write. Items are ordered by what blocks the most.

## A. Language gaps (the compiler source cannot be written without these)

1. **DONE (iteration 4, nested case): `match` as an expression anywhere.** A `match` as the last statement already worked; a case
   can now be a `match` whose cases are indented two spaces deeper (`examples/expr/simplify.fn`, `tests/errors/match-incomplete-nested`).
   Still not allowed: a `match` inside an `if` branch or as an operand. Original problem: Today a `match` must start a line or follow `let x =` (`Parser.cs`, error `syntax`).
   A parser/checker is mostly nested matches and matches that return the function result. Allow: `match` as the last
   statement (the return value), and a `match` arm that is itself a `match` block (or the arm calls a helper, which costs one
   file per arm). Without this the AST passes need hundreds of tiny helper files.
2. **Function pointers + `listMap` / `listFilter` / `listFold` / `listSort`** (decided in `function-pointers-T-0928.md`, not built).
   Every pass over a list is hand-written recursion today.
3. **`Result<T, E>` / `Option<T>`, hence generic variants** (decided in `open-questions-T-0944.md` item 5, not built). The parser
   and checker must return "value or error"; today that needs a struct with an `error` field per stage.
4. **DONE (iteration 4): recursive and list-containing variants.** `examples/expr` has `Expr` with `Neg(inner Expr)` and `Sum(items List<Expr>)`;
   build, `aip test`, lint and fuzz pass. Original problem: An AST is `Expr` containing `List<Expr>`. The type-erased `List` probably allows
   it, but no example proves it. Needed first: a 10-line example (`variant Expr`, `Add(left Expr, right Expr)`, `Call(name Text, args List<Expr>)`)
   with a test; fix whatever fails.
5. **Struct/variant values as `Map` / `SortedMap` keys, and structural `==` on them.** The checker needs symbol tables keyed by
   names (Text, works) and sets of types (struct/variant, does not).

## B. Standard library gaps

| Need | Why the compiler needs it | Proposed std function |
|---|---|---|
| Join many texts | the emitter builds large outputs; repeated `+` is quadratic | `textJoin(parts List<Text>, separator Text)` |
| Split / search text | line splitting, finding `->`, `:` in headers | `textSplit`, `textIndexOf`, `textStartsWith`, `textTrim` |
| Int to Text and back | positions, error messages (`{n}` interpolation covers Int to Text; `textToInt` exists) | none |
| More list operations | symbol tables, token windows | `listSet`, `listSlice`, `listConcat`, `listReverse` |
| Write many files, make folders | the cache holds one object file per function | `fileMakeFolder`, `fileExists` (`file.write`, `file.read`) |
| List a folder | project loader finds every `.fn`, `.struct`, `.variant`, `.const` | `fileList(folder Text) -> List<Text>` (`file.read`) |
| Bytes and binary files | IR, object code and the exe are not text | `Bytes` type, `fileReadBytes`, `fileWriteBytes`, `bytesGet/Set/Append/Slice`, Int bit operations (`and`/`or`/`xor`/shift on Int) |
| Exit code | `aip check` returns non-zero on errors | `processExit(code Int)` (`process.exit`) |
| Std implementations | `Externs` in `CsBackend.cs` are C# text | each std function gets a native implementation in the runtime (section E) and an IR-level definition; `check-externs.ps1` compares header, IR definition and interpreter |

## C. Compiler stages still to write in AIP (in order, each with a compare script like the lexer and header parser)

1. **Body parser** (statements, expressions, `match` arms, string `{expr}` parts) and parsers for `.struct`, `.variant`,
   `.const`, `requires` / `ensures` / `example`. Check: `aipc ast <file>` equals a new C# `aip ast <file>` dump.
2. **Project loader**: walk the folder, apply the one-definition-per-file and file-name rules, merge std. Check: same file
   list and index as `aip index` (already compared by `compare-index.ps1`).
3. **Checker** subset: names, types, effects, reachability, constants, std layout. Start with the error codes the AIP compiler's own
   source can trigger; compare against `tests/errors` one code at a time (`check-error-coverage.ps1` already lists them).
4. **IR lowering**: checked program to the IR (section E), one IR unit per function. Check: a textual IR dump equal to the dump
   of the C# front end for every example.
5. **Native code generator + linker**: IR unit to machine code, units to an exe (section E). Check: every `examples/*/expected-output.txt`
   passes with the exe built without C# or `dotnet`.
6. **Driver**: `aipc build <dir>` loads, checks, lowers only changed units, links. No `processRun` needed.
7. **Stage check**: the C# compiler builds `aipc` (stage 1); stage 1 builds `aipc` (stage 2); stage 2 output is byte-equal to
   stage 1. After that the C# compiler is only a seed, kept in the repo to rebuild from nothing.

Subset rule to keep this small: the AIP compiler need only accept the language the AIP compiler is written in. Lint, `aip test`,
query, fuzz, `--target` and `aip find` stay in C# until a later ticket. The C# back end (`CsBackend.cs`) stays as the seed and as the reference for output until step 7 passes, then is removed.

## E. No C# in the middle: IR, cache, exe (feedback on iteration 1)

Decision: the AIP compiler goes source -> **AIP IR** -> **machine code** -> **exe**. No C# text, no `dotnet` at build time.

**IR.** A small typed, first-order, immutable format: one *unit* per AIP function (this fits "one function per file"). A unit holds
the function's typed body in a flat, stack-free form (let-bound values, calls, `if`, `match`, constants) plus the names and
signatures it calls. Binary and deterministic, so units can be hashed and compared byte for byte.

**Cache.** Directory `build/cache/`, one file per unit, named by hash. The key of a unit is
`hash(function source, hash of the signature of every function/type/const it uses, compiler version, target)`.
Only the *signature* of callees is in the key, not their bodies, so editing a body recompiles that one function; editing a signature
recompiles it and its callers. Because effects are in the signature, an effect change correctly invalidates callers. Check phase
results (`check`/`index`) use the same keys. Safe to delete; a damaged entry is rebuilt. Test: change one function body in a
100-function project and assert exactly one unit is regenerated (counted in `AIP_TIMINGS=1` output).

**Exe.** Three options, in order of cost:
1. *Stub + IR payload.* The exe is a prebuilt runtime that holds the IR and interprets/JITs it. Fast to build, no code generator
   needed, slow to run, and not the compiler's own speed target. Good as a first milestone (it makes the stage check possible
   early).
2. *Own x64 code generator + own linker/PE writer (Windows, later ELF for `linux-x64`).* Each unit becomes position independent code
   with relocations; the linker concatenates cached units, resolves calls, appends the runtime, writes the PE. Real native speed,
   fully cached, no external tools. Most work.
3. *Use an existing back end* (QBE, LLVM, or the system assembler/linker). Least work for good code, but adds a non-AIP tool dependency
   that conflicts with "no intermediary" and with the supply-chain rule in `security-T-0936.md`.

Recommendation: 1 then 2. Ship option 1 as soon as the IR exists, then replace the stub with generated code unit by unit (a unit may
be native or interpreted in the same exe), so the exe never stops working. Skip 3 unless 2 stalls.

**Runtime.** Everything the generated code needs that is not a function: memory allocation and a garbage collector (all values are
immutable and acyclic, so reference counting or an arena per call tree is enough, no cycle collector), text and collection
implementations behind the std functions, the deep stack (the current 512 MB), file/terminal/args/log effects as OS calls
(`kernel32` on Windows, syscalls on Linux), and the crash report (`runtime error:`, `AIP_TRACE`). It is written in AIP plus a very small
set of *intrinsics* (raw memory load/store, OS call) that only the runtime folder may use, so the language the user sees stays free of
`unsafe` (`compiler.md` rule). That runtime is the largest single item in this plan after the code generator.

## D. Risks

- **Speed and memory.** The lexer already needs a 512 MB stack because loops are recursion. A 150-file source with immutable
  `List` copies on every append (`listAppend` returns a new list) may be too slow. Measure `aipc tokens` on the whole compiler first;
  if slow, make `listAppend` / `mapSet` use persistent structures in the back end (no language change).
- **Runtime in AIP.** Memory management and collections need raw memory intrinsics. Keep them in one folder, with an effect name
  of their own, so no ordinary program can reach them.
- **Two machine targets.** Windows PE and Linux ELF writers are separate work; do Windows first, since the stage check runs there.
- **Size per file.** One function per file will make the AIP compiler several hundred files; fine for the index, but `match`
  helpers (A1) would double it.
- **Open question 11** (`open-questions-T-0944.md`): self-hosting competes with the benchmark and the error-quality work. Bootstrapping is
  worth it mainly because the compiler then becomes the first large test of whether AI can write AIP; a go/no-go after A1-A4 is reasonable.

## Recommended order

1. A1 (`match` anywhere) and A4 (recursive variant example): small, unblock the body parser.
2. B: `textJoin`, `textSplit`, `textIndexOf`, `textStartsWith`, `listSet`, `listSlice`, `listConcat`, `fileList`. All pure or simple I/O.
3. Body parser with its compare script (C1).
4. A2 (function pointers) and A3 (`Result` / `Option`) before the checker (C3), since the checker is where they pay off.
5. `Bytes` and bit operations, IR, runtime, code generator, linker, driver, stage check (C4-C7).

## F. Are we ready? Gates before the C# compiler is removed (feedback on iteration 2)

Answer: ready to *start* building the AIP compiler, not ready to *remove* the C# one. Removing it is the one step that is hard to
undo, so it comes last and only after the gates below pass. Until then nothing is lost by building the AIP compiler beside it.

Why reversing is painful, and how to avoid it:
- After self-hosting, a new language feature must be written in AIP, in a compiler that cannot yet use that feature. The usual
  fix is two steps: add the feature to the compiler without using it, rebuild (stage N+1), then use it. This is normal and cheap
  when the compiler build is fast (the cache in section E), but it needs a trusted *seed* to start from.
- A language mistake found late (for example `match` rules, the IR format) costs more once two compilers and the IR exist.

Mitigations (all cheap, do them at the start):
1. **Keep the C# compiler** in the repo, frozen (bug fixes only), until the gates pass. Tag the last commit that has it (`seed-csharp`).
2. **Check in a seed exe** of each released AIP compiler (or keep it as a build artifact with its hash in the repo), so the compiler can
   be rebuilt from the previous one. Never depend on a compiler that cannot be rebuilt from the repo plus the seed.
3. **Freeze the IR with a version number** and a test that old cached units are rejected, not misread.

Gates (all must hold; each is a test in `tests/checks/`):
1. Section A items 1-4 done: `match` anywhere, function pointers, `Result`/`Option`, a proven recursive variant. These change how the
   compiler itself is written, so they must exist before it is written, not after.
2. Section B std functions done, including `Bytes` and bit operations.
3. Every `examples/*/expected-output.txt` and every `tests/errors`, `tests/lint`, `tests/contracts` case passes with the AIP compiler.
4. Stage 2 output is byte-equal to stage 1 (`tests/checks/bootstrap-stages.ps1`).
5. Warm rebuild after a one-function edit regenerates exactly one unit, and a full self-build finishes in a time we accept
   (measure first; suggestion: under 60 seconds cold).
6. Everything the C# compiler does and users rely on exists in AIP or is explicitly dropped by a decision file: `lint`, `test`, `query`,
   `find`, `fuzz`, `spec`, `profile`, `--target`. Missing ones are listed, not forgotten.
7. One feature is added to the language *using only the AIP compiler* as a rehearsal (for example `textTrim`), proving the two-step
   workflow before the C# compiler goes.

Open design choices to settle first, because they are expensive to change after: the IR format (A1/E), how effects and ranges are encoded
in a unit, the `Bytes` and number decisions (open question 9: overflow, `Float`), and the function-pointer representation in native code.

Recommended: start with section A and the body parser now (the C# compiler still checks everything), write the IR spec as its own
design file and review it before any code generator, and decide the removal date only when gates 1-7 are green.

## G. Progress log

- Iteration 4: gates F.1 partly met (A1 nested `match`, A4 recursive variant). Fixed the AIP header parser (`examples/aipc/parse/parseType.fn`) to read
  `SortedMap`/`SortedSet` types; `scripts\compare-index.ps1` had been failing on the standard library since T-0951 added them.
  Next: B (text/list/file std functions), then the body parser. Tag `seed-csharp` on the commit that is merged (Architect owns git, so it is left to the user).
