# Road to bootstrapping (compiler written in AIP)

Done: `struct`, `variant`, `match`; `List<T>` (type-erased at run time), text helpers (`textLength`, `textSlice`, `textCode`, ...), file and
argument access, Unit `if` statements, deep recursion (512 MB stack), `.const` files (`docs/design/consts-T-0941.md`), `Map` and `Set`.

The compiler written in AIP is `examples/aipc` (one project; one subfolder per stage):
- `lex/`: the lexer. `aipc tokens <file>` prints the tokens; `scripts\compare-lexers.ps1` checks they equal the C# lexer's
  (`aip tokens <file>`) for every `.fn` and `.const` file, including its own source.
- `parse/`: the header parser (function and constant headers into `FnHeader`: name, parameters with types and ranges,
  return type, purpose line, effects). `aipc index <file>...` prints one index line per file; `scripts\compare-index.ps1`
  checks they equal `aip index` for every example and `aip std` for the standard library.
Both checks run in `scripts\test.ps1` (`tests\checks\bootstrap-lexer.ps1`, `tests\checks\bootstrap-parser.ps1`).

Still missing before the compiler can compile itself:
1. Parser for bodies (statements and expressions into an AST, as variants), and for `.struct`/`.variant` files and
   `requires`/`ensures`/`example` lines. Check: print the AST and compare with a C# dump.
2. Checker in AIP (names, types, effects, reachability, the error codes of `tests/errors`).
3. Emitter that writes the C# text; compare with the C# back end's output for every example.
4. A way to run `dotnet publish` (a `process.run` effect) from AIP.
5. Then: C# compiler builds the AIP compiler (stage 1), stage 1 builds itself (stage 2), stage 2 output must equal stage 1.

The native Windows exe compiler is also written in AIP: `aipc native <dir> <out.exe>`, see `docs/design/native-compiler-in-aip-T-1033.md`
(`tests\checks\bootstrap-native.ps1`).
