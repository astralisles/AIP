# Working on the compiler

- Overview, commands and what the language supports today: `compiler/README.md`. Read it before changing the compiler.
- Front end (`Parser.cs`, `Checker.cs`) stays target independent; target-specific code goes in a back end (`CsBackend.cs`).
- Standard library: `compiler/std/**/*.fn`, header-only `extern` functions, one per file; the C# implementation is in `CsBackend.Externs.*.cs` (see `docs/claude/source-layout.md`).
- Before adding a language feature, read the related decision in `docs/design/` (global state, function pointers,
  parallelism, security). The security rule applies to every feature: anything that reaches outside the program
  is an effect in the signature; never add `unsafe`, FFI, reflection, `eval` or mutation outside the `mutate` effect.
- Error messages are tested by `tests/errors`; keep the error code (for example `missing-doc`) stable.
