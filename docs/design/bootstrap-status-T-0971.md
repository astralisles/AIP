# What else do we need to bootstrap, and the `Int` name (T-0971)

## Decision (iteration 2)
`Int` is gone: it was renamed `I64` everywhere, with no alias. Every whole-number type now names its bits
(`I8 I16 I32 I64 U8 U16 U32 U64`). Writing `Int` is the error `unknown-type`, whose hint says it was renamed
(test `tests/errors/int-renamed`). Semantics are unchanged (64-bit signed, same widening rules). The lexer's token kind `Int`
(a number literal) is not a type and keeps its name.

## Done for the bootstrap
Examples `aipc`: lexer, header parser, body parser (flat dump), compared with the C# compiler by `scripts\compare-*.ps1`.
Std added in this ticket (example `fileio`, check `tests/checks/fileio.ps1`): `fileList`, `fileExists`, `fileMakeFolder`,
`fileReadBytes`, `fileWriteBytes`, `processExit`.

## Still needed, in order
1. **Language gaps:** function pointers with `listMap/Filter/Fold/Sort` (A2), `Result<T,E>` / `Option<T>` (A3; generic
   variants exist since T-0949, the std use is not written).
2. **Body parser leftovers (C1):** `.struct`, `.variant`, `.const` files, `native` line, signature in the tree, nested
   statement tree, parse errors with the `syntax` codes.
3. **Project loader (C2), checker (C3), IR lowering (C4), code generator + linker (C5), driver (C6).**
4. **Stage check (C7):** stage 2 output byte-equal to stage 1; `scripts\bootstrap.ps1` turns its SKIP into a check.
5. Gates before removing C#: `bootstrap-gaps-T-0954.md` section F.

Biggest unknowns: the runtime (memory, GC), the native code generator, speed of immutable `listAppend` on 150 files.
Open: plain `I64` arithmetic still wraps silently while the smaller types are checked (open question 9).
