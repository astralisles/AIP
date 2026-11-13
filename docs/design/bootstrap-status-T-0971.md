# What else do we need to bootstrap, and is `Int` the right name? (T-0971)

## Status today (checked in the repo)

Done in `examples/aipc`: lexer, header parser, body parser (expressions, `requires`/`ensures`/`example`, flat dump). Compared with
the C# compiler by `scripts\compare-*.ps1`; `scripts\bootstrap.ps1` also shows aipc reading its own source. Stage 2 is still SKIP.
Std now has `textJoin/Split/StartsWith/Trim/Replace/Find`, `listSet/Slice/Concat/Reverse`, `bytes*`, `bit*`.

## Still needed, in order

1. **Std gaps:** `fileList`, `fileMakeFolder` / `fileExists`, `processExit`, `fileReadBytes`/`fileWriteBytes` (only `fileRead`, `fileWrite`
   and `arg`/`argCount` exist). The project loader and the exe writer need them.
2. **Language gaps:** function pointers with `listMap/Filter/Fold/Sort` (A2), and `Result<T,E>` / `Option<T>` with generic variants (A3).
   Neither exists yet; the checker should not be written before them.
3. **Body parser leftovers (C1):** `.struct`, `.variant`, `.const` files, `native` header line, signature in the same tree,
   nested statement tree (today the dump is flat), parse errors with the `syntax` codes.
4. **Project loader (C2), checker (C3), IR lowering (C4), code generator + linker (C5), driver (C6).**
5. **Stage check (C7):** stage 2 output byte-equal to stage 1; `scripts\bootstrap.ps1` turns its SKIP into a check.
6. **Gates before removing C#:** see `bootstrap-gaps-T-0954.md` section F.

Biggest unknowns: the runtime (memory, GC) and the native code generator; speed of immutable `listAppend` on a 150-file source.

## Should `Int` be renamed `I32`?

No. In AIP `Int` is **not** C's variable-size `int`; it is the fixed 8-byte signed integer (`data-types-T-0945.md`), i.e. what other
languages call `I64`/`int64_t`. `I32` is already a separate 4-byte type.

Why `Int` exists:
- It is the everyday default: literals, `Text` lengths, list indexes, `textToInt` and `{n}` interpolation all use it. The widening
  rules (`I8`/`I16`/`I32`/`U8`/`U16`/`U32` widen to `Int`) make it the meeting point of all whole-number types.
- It is not a "generic integer": sized types are not implicitly narrowed to it, and `Int` is not polymorphic.
- Renaming it `I32` would shrink every existing program to 32 bits (text offsets and file sizes in the compiler can pass 2 GB only
  rarely, but overflow of plain `Int` wraps silently today) and break ~all examples, tests and the 67 aipc files that use `Int`.

Options if the name is the concern (not recommended without a decision):
1. Keep `Int` = 64-bit (recommended). Document it in the first line of `compiler/README.md` (already in the data-types note).
2. Add an `I64` alias for symmetry. The design rejected it ("one name per type").
3. Rename `Int` to `I64` everywhere: a mechanical but wide change; the only gain is that every integer name states its size.

One related item worth deciding: plain `Int` arithmetic wraps silently while sized types are checked. For a compiler this is risky
(hash and offset code). Suggest making `Int` overflow a run-time error too (open question 9).
