# `if` without `else`, and removing `noop()` (T-1036)

Decision: an `if` may leave out `else` when its `then` part is a Unit statement.
`noop()` is removed from std (`compiler/std/core/noop.fn`, the C# and interpreter externs, `Rt.Nop`).

- One line: `if n > 0 then printAndRepeat(n)`. Block form: `if c then` and indented statements, no `else` line.
- `else if` chains already worked (the else part is any expression) and still do.
- Parser: a missing `else` becomes an empty `BlockExpr` and `IfExpr.NoElse = true`; the checker, back ends and interpreter see an
  ordinary Unit `else` that does nothing, so they needed no change.
- Checker: if the `then` part is not Unit the error is `missing-else` (code kept, tested by `tests/errors/missing-else`).
- Dangling else: `if a then if b then x else y` gives the `else` to the inner `if`, as in C.
- `aip ast` prints `(if c t (skip))` for a one-line `if` without `else`, and omits the `else` line for a block; the AIP-written
  parser (`examples/aipc`, case `NoElse` of `Expr`) prints the same.
- Test: `examples/oneArmedIf`.
