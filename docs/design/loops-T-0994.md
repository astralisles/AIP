# Loops (T-0994)

Decision: loops are grammar, not a function. One `loop` form for values, one `for` for effects.

```
let total = loop          (or a bare `loop` as the last line of a function)
  r = 1                   one `name = start` line per loop value
  total = 0
  body
    let rowTotal = ...    `let`s and Unit statements
    if r > rows then done total else next r + 1, total + rowTotal

for r in 1..rows          r runs from 1 up to rows - 1; the body is Unit statements
  printLine("{r}")
```

## Rules
- Every loop value goes on its own line, so the state reads top to bottom (what works best for AI: no comma lists to count).
- The last line of a `body` is the step: an `if` chain whose leaves are `done value` (leave, the loop is worth that value)
  or `next a, b` (run again; the next value of every name, in declared order).
- Nesting is by indentation: `let x = loop` or `for` inside a body. No labels, no `break`/`continue`.
- `done` and `next` are keywords only inside a loop body (`next` is still a normal name elsewhere, e.g. `examples/aipc`).
- Loop names and `for` variables cannot be reused by a later `let` in the function (the C# back end declares them in one scope).
- `for` bounds are I64; the end is exclusive. The body may only hold Unit statements and `let`s.

## Errors
`loop-no-body`, `loop-no-state`, `empty-body`, `loop-state-count`, `loop-no-exit`, `loop-misplaced`, plus the existing
`type-mismatch`, `duplicate-name`, `unused-let`, `unused-value`. Each has a case in `tests/errors`.

## Implementation
Parser.Loop.cs, Checker.Loop.cs, Interp.Loop.cs, CsBackend.Loop.cs, Model.Loop.cs. The C# back end emits a `while(true)` inside
a lambda for `loop` and a `for` statement for `for`. Recursion with tail calls (T-0983) still works and is unchanged.
