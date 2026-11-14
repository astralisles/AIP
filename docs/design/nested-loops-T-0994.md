# Why not have loops? Are nested loops difficult? (T-0994)

Answer: **no, nested loops are not difficult with recursion; the decision in `loops-and-switches-T-0975.md` stands.**

- Each loop is its own function. A nested loop is the inner loop's function called from the outer one, passing the
  running state. Nothing special is needed, and each loop has its own name, doc line, `example` and index line.
- No hidden state: the counters of both loops are visible parameters (`r`, `c`, `total`), so there is no
  "which counter did I forget to reset" bug, the classic nested `for` mistake.
- Runnable proof: `examples/nested-loops` (`tableSum` calls `rowSum`; `printTable` calls `printRow`), checked by `scripts\test.ps1`.

## The real cost
- Verbosity: a nested loop needs two functions and passes state by hand. One function per file makes this more files, not harder code.
- Stack depth: depth is the sum, not the product, of the loops (the inner call returns before the outer continues),
  but long loops still need tail-call lowering (step 1 in `loops-and-switches-T-0975.md`, not done yet).
- If it still reads badly, the step-3 sugar `for x in list` would desugar to the same two functions; nesting is just two of them.

Not done: no compiler change. Only an example and this note were added.

## Follow-up: would loop grammar read better than a loop-is-a-function? (feedback on T-0994)

Fair point: recursion is checkable, but a reader must find the helper functions and see that `rowSum` is "the inner loop".
Syntax that shows the loop in place would be easier to read. Proposal that keeps the rule "state is visible, values are immutable":
a loop is an expression that names its counter and its state, and its value is the final state. Nesting is indentation.

Today (two files, `examples/nested-loops`):

    tableSum(r, rows, cols, total) = if r > rows then total else tableSum(r + 1, rows, cols, rowSum(r, 1, cols, total))
    rowSum(r, c, cols, total)      = if c > cols then total else rowSum(r, c + 1, cols, total + r * c)

Option A, `fold` (value loop, state is declared in the header):

    fold r in 1..rows with total = 0
      fold c in 1..cols with total = total
        total + r * c

The body of a `fold` is the next state. The inner `fold` starts from the outer state, so nothing is reset by hand.

Option B, `for` (Unit loop with an effect, no state):

    for r in 1..rows
      for c in 1..cols
        printLine("{r * c}")

Both desugar to the same recursive functions (generated, so tail-call lowering from T-0975 applies) and the function's
`effects` line covers them. Costs: a new indented block grammar (today blocks do not nest), the generated helper
functions are not in the one-line index, and there are now two ways to write a loop.
Decision needed from the user; no compiler change made yet.
