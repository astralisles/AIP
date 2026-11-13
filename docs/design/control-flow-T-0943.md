# Control flow: conditionals and loops (T-0943)

- Conditionals: `if c then a else b` is an expression on one line, chainable with `else if`; with Unit branches
  it is a statement. `and`, `or`, `not` and `match` also work. A multi-line `if` block does not parse yet, so a
  branch that needs two statements calls a helper function (`examples/control/countdownStep.fn`).
- Loops: yes, loops are done with recursion. There is no `while`/`for`, because values are immutable: a loop is a
  function that calls itself with the next state in its parameters, e.g. `sumTo(n - 1, total + n)`.
  One million iterations run fine on the 512 MB stack (`examples/control`, checked by `scripts\test.ps1`).

Possible next steps (not done):
1. `while`/`for` syntax that compiles to the same recursion.
2. `listMap`/`listFold` once `Fn` pointers exist (`function-pointers-T-0928.md`).
3. Turn self-calls in tail position into real loops in the back end, so tens of millions of iterations cannot overflow the stack.
4. Multi-line `if` blocks.
