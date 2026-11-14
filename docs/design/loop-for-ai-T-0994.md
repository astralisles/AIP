# Which loop syntax works best for AI? (T-0994)

Recommendation: **one construct, `loop` with state on separate lines (`loop-state-lines-T-0994.md`), plus `for` for effect-only loops. No `fold`, no higher-order loops yet.**

What helps a model writing and reading code, in order of weight:
1. **Few ways to say the same thing.** Models copy patterns; two loop forms means drift and mixed styles. One value loop (`loop`) is enough; `for` is kept only because an effect loop has no state to name.
2. **State visible and every variable named.** The common model errors in loops are a forgotten update and a wrong initial value. With `next` required to give every variable (checked, error `loop-state-count`) both become compile errors with a clear message, not wrong output.
3. **Explicit exit.** `done value` makes the result and the stopping point obvious; `fold r in 1..rows` hides both and cannot do early exit, so code would be rewritten into a second form later.
4. **No counting tricks.** No `i++`, no mutable counters: nothing to forget, and each iteration is one `next` line, which is easy to generate and check.
5. **Small, local, regular.** Keywords at line start, one item per line, indentation for nesting: easy for a model to generate token by token and for a diff to show.
6. **Nesting.** Keep it flat: bind the inner loop in a `let` line before the `if`, or call a named function. Deep nesting inside `next` is where models make mistakes.

Why not the others: `fold` (second form, no early exit), higher-order functions (need lambdas; nested closures are where models get arguments in the wrong order), plain recursion (works, but the loop shape is hidden in an `if` and the state is spread over helper functions).
Everything still desugars to recursion, so tail-call lowering applies; do tail-call lowering first, it needs no syntax decision.
