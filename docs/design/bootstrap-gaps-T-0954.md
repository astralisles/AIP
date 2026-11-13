# What else is needed to bootstrap the compiler (T-0954)

Status today: `examples/aipc` has the lexer and the header parser, both checked against the C# compiler on every test run
(`docs/design/bootstrapping.md`). The C# compiler is about 2,700 lines; the part that must be rewritten to compile itself is
`Parser.cs` (592), `Checker.cs` (546), `CsBackend.cs` (469), `Project.cs` (102) and `Program.cs` (181). `Lint`, `Interp`, `Query`,
`Fuzz` and `SourceCache` are not needed to bootstrap and stay in C# (or follow later).

Rule used below: the AIP compiler may only use features AIP has, so every gap is either a language or std feature the
compiler source needs, or a compiler stage still to write. Items are ordered by what blocks the most.

## A. Language gaps (the compiler source cannot be written without these)

1. **`match` as an expression anywhere.** Today a `match` must start a line or follow `let x =` (`Parser.cs`, error `syntax`).
   A parser/checker is mostly nested matches and matches that return the function result. Allow: `match` as the last
   statement (the return value), and a `match` arm that is itself a `match` block (or the arm calls a helper, which costs one
   file per arm). Without this the AST passes need hundreds of tiny helper files.
2. **Function pointers + `listMap` / `listFilter` / `listFold` / `listSort`** (decided in `function-pointers-T-0928.md`, not built).
   Every pass over a list is hand-written recursion today.
3. **`Result<T, E>` / `Option<T>`, hence generic variants** (decided in `open-questions-T-0944.md` item 5, not built). The parser
   and checker must return "value or error"; today that needs a struct with an `error` field per stage.
4. **Recursive and list-containing variants.** An AST is `Expr` containing `List<Expr>`. The type-erased `List` probably allows
   it, but no example proves it. Needed first: a 10-line example (`variant Expr`, `Add(left Expr, right Expr)`, `Call(name Text, args List<Expr>)`)
   with a test; fix whatever fails.
5. **Struct/variant values as `Map` / `SortedMap` keys, and structural `==` on them.** The checker needs symbol tables keyed by
   names (Text, works) and sets of types (struct/variant, does not).

## B. Standard library gaps

| Need | Why the compiler needs it | Proposed std function |
|---|---|---|
| Join many texts | the emitter builds large C# files; repeated `+` is quadratic | `textJoin(parts List<Text>, separator Text)` |
| Split / search text | line splitting, finding `->`, `:` in headers | `textSplit`, `textIndexOf`, `textStartsWith`, `textTrim` |
| Int to Text and back | positions, error messages (`{n}` interpolation covers Int to Text; `textToInt` exists) | none |
| More list operations | symbol tables, token windows | `listSet`, `listSlice`, `listConcat`, `listReverse` |
| Write many files, make folders | the emitter writes one `.cs` per function and a `.csproj` | `fileMakeFolder`, `fileExists` (`file.write`, `file.read`) |
| List a folder | project loader finds every `.fn`, `.struct`, `.variant`, `.const` | `fileList(folder Text) -> List<Text>` (`file.read`) |
| Run a process | stage 1 must call `dotnet publish` and run the result | `processRun(command Text, args List<Text>) -> RunResult` (effect `process.run`; `security-T-0936.md` applies: the effect appears in `main` and in the index) |
| Exit code | `aip check` returns non-zero on errors | `processExit(code Int)` (`process.exit`) |
| Read std headers and the C# templates | the `Externs` templates in `CsBackend.cs` are C# string literals | keep them as data files next to the headers (for example `std/<group>/<name>.cs.txt`) that both compilers read; this also removes the duplicate between `check-externs.ps1` and the back end |

## C. Compiler stages still to write in AIP (in order, each with a compare script like the lexer and header parser)

1. **Body parser** (statements, expressions, `match` arms, string `{expr}` parts) and parsers for `.struct`, `.variant`,
   `.const`, `requires` / `ensures` / `example`. Check: `aipc ast <file>` equals a new C# `aip ast <file>` dump.
2. **Project loader**: walk the folder, apply the one-definition-per-file and file-name rules, merge std. Check: same file
   list and index as `aip index` (already compared by `compare-index.ps1`).
3. **Checker** subset: names, types, effects, reachability, constants, std layout. Start with the error codes the AIP compiler's own
   source can trigger; compare against `tests/errors` one code at a time (`check-error-coverage.ps1` already lists them).
4. **Emitter**: same C# text as `CsBackend.cs`. Check: byte-equal output of `aip emit` for every example (`check-determinism.ps1`
   already shows the C# output is deterministic, which makes a byte compare possible).
5. **Driver**: `aipc build <dir>` writes the files and runs `dotnet publish` through `processRun`.
6. **Stage check**: C# builds `aipc` (stage 1); stage 1 builds `aipc` (stage 2); stage 2 emits the same C# as stage 1.
   Add as `tests/checks/bootstrap-stages.ps1`.

Subset rule to keep this small: the AIP compiler need only accept the language the AIP compiler is written in. Lint, `aip test`,
query, fuzz, `--target` and `aip find` stay in C# until a later ticket.

## D. Risks

- **Speed and memory.** The lexer already needs a 512 MB stack because loops are recursion. A 150-file source with immutable
  `List` copies on every append (`listAppend` returns a new list) may be too slow. Measure `aipc tokens` on the whole compiler first;
  if slow, make `listAppend` / `mapSet` use persistent structures in the back end (no language change).
- **Size per file.** One function per file will make the AIP compiler several hundred files; fine for the index, but `match`
  helpers (A1) would double it.
- **Open question 11** (`open-questions-T-0944.md`): self-hosting competes with the benchmark and the error-quality work. Bootstrapping is
  worth it mainly because the compiler then becomes the first large test of whether AI can write AIP; a go/no-go after A1-A4 is reasonable.

## Recommended order

1. A1 (`match` anywhere) and A4 (recursive variant example): small, unblock the body parser.
2. B: `textJoin`, `textSplit`, `textIndexOf`, `textStartsWith`, `listSet`, `listSlice`, `listConcat`, `fileList`. All pure or simple I/O.
3. Body parser with its compare script (C1).
4. A2 (function pointers) and A3 (`Result` / `Option`) before the checker (C3), since the checker is where they pay off.
5. `processRun`, `processExit`, emitter, driver, stage check (C4-C6).
