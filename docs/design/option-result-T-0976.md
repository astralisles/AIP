# Option and Result in std (T-0976)

Decision source: `open-questions-T-0944.md` item 5. Built so far:

- `compiler/std/core/Option.variant` (`Some(value)`, `None`) and `Result.variant` (`Ok(value)`, `Err(error)`).
  Std types are loaded like std functions; the csproj copies `std/**/*.variant`.
- `listTryGet(items, index) -> Option<T>` and `mapTryGet(m, key) -> Option<V>`, written in AIP (a std function with a
  body). The C# back end emits bodied std functions; `scripts/check-externs.ps1` skips them.
- The old `listGet`/`mapGet` are unchanged (still crash); the new functions are additive, so nothing breaks.

Breaking: `Option`, `Result`, `Some`, `None`, `Ok`, `Err` are now taken, so a project defining its own gets `duplicate-name`.
`examples/generics/Option.variant` was removed and now uses the std one.

Not done: extern try-versions that catch a failure (`textTryToInt`, `fileTryRead`, ...) and a `?` shorthand.
