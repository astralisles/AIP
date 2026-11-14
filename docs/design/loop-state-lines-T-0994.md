# `loop` with one state variable per line (T-0994)

Feedback: in `loop r = 1, total = 0` the state should be on its own lines. Agreed: one variable per line is easier to
read and to diff, and a long state list does not run off the line. Not implemented yet; syntax proposal only.

## Rules
- `loop` alone on its line, then one `name = start` per line, indented 2 more.
- A line with only `do` (or a blank-free dedent) starts the body, indented the same as the state lines is NOT allowed; the body is indented 4.
- In the body, `next a, b` gives the next value of every variable, in declared order; `done value` ends the loop with its value.
  `next` must give every variable (error `loop-state-count`), and `done` and `next` must be the last thing on a path (error `loop-no-exit`).

## Sum 1..n
    loop
      i = 1
      total = 0
    body
      if i > n then done total
      else next i + 1, total + i

## Nested table sum
    loop
      r = 1
      total = 0
    body
      if r > rows then done total
      else next r + 1, loop
        c = 1
        t = total
      body
        if c > cols then done t
        else next c + 1, t + r * c

## Compared with `fold` (style 2 in `loop-syntax-examples-T-0994.md`)
`fold r in 1..rows with total = 0` is shorter for counting loops; `loop` also covers early exit and steps other than +1.
Both can exist: `fold` and `for` desugar to `loop`, and `loop` desugars to recursion.
