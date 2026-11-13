# Should we allow loops or switches? (T-0975)

Answer: **switches yes (already there as `match`), loops no as syntax; fix the cost of recursion in the back end instead.**

## Switches
- `match` already is the switch: one `Case(a, b) => expr` arm per variant, exhaustive, an expression or a Unit statement.
  A C-style `switch` on integers or strings adds nothing a chain of `else if` does not, and it loses the exhaustiveness check.
  Decision: no `switch` keyword. Do not add it.
- Possible later: `match` on literals (`0 => ...`, `"a" => ...`) with a required `_` arm, only if real code needs it.

## Loops
- `while`/`for` need a mutable counter or a hidden state, which breaks "values are immutable, `mutate` is an effect in the
  signature" (`global-state-T-0925.md`). A loop that hides its state also hides it from the one-line index.
- Recursion already covers every loop (`control-flow-T-0943.md`). The real problem is stack depth (512 MB stack today, and the
  bootstrap lexer relies on it, `bootstrap-gaps-T-0954.md`), not the syntax.
- Decision: no `while`/`for` keywords. Instead, in order:
  1. Back end turns self-calls in tail position into real loops (no language change, no stack growth). Do this first.
  2. Higher-order std functions (`listMap`, `listFilter`, `listFold`) once `Fn` pointers exist (`function-pointers-T-0928.md`);
     they cover most "for each item" code without a loop keyword.
  3. Only if 1 and 2 still read badly in the AIP-written compiler, revisit sugar such as `for x in list` that desugars to
     the recursion above. It must keep state visible in the signature.

## Why
AIP is designed for AI: fewer ways to say the same thing, every function's state visible in its signature, and no
off-by-one or forgotten-increment class of bugs. Recursion with named parameters is checkable; tail-call lowering makes it cheap.

No code was changed; this is a design decision. Recommend merging the note.
