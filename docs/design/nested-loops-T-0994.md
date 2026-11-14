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
