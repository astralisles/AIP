# More restraints on functions for static analysis (T-0966)

Question: what other restraints could we add to functions to help static analysis?

## Already there

Effects list (`pure` or `effects a, b`), typed parameters with ranges, `requires`, `ensures`, `example`, one-line doc,
no globals, no mutation, no loops, no aliasing, every function reachable from `main`. Contracts are tested, not proved
(`contracts-T-0950.md`).

## Principle

A restraint is worth adding when (a) the checker can verify it cheaply, (b) it is stated in the signature so a caller or model
needn't read the body, and (c) it removes a class of "cannot prove" answers. Every restraint is a header line,
optional unless said otherwise, and a violation is an error only when it is decidable without a prover; otherwise a lint warning.

## Candidates, best value first

1. **`cannot-fail`** (computer-science name: a "total function"; renamed because "total" is confusing). Declares: no `requires` can fail, no division by zero, no out-of-range index, terminates.
   Checkable with the interval analysis planned in T-0950 step 2 plus `decreases`. Callers of a `cannot-fail` function need no
   proof obligations. Index shows `cannot-fail`. Highest value; build after T-0950 steps 2-3.
2. **`decreases expr` (termination).** Already planned (T-0950 step 3). Prerequisite for `cannot-fail`. Replaces the
   `unbounded-recursion` heuristic.
3. ~~Scoped effects (`file.read(root)`)~~ **Dropped at the user's request (T-0966 #4).** Path scoping stays a security topic (`security-T-0936.md`), not a function restraint.
4. **Nullability and failure in the type, not a flag.** Already `Option`/`Result`. Restraint: a function that can fail
   must say so with `fails` in the header or return `Result`; `requires` is then the only crash source. Lint
   `hidden-failure` for std calls that can crash (`listGet`) used in a function not marked `fails`.
5. **`ensures` shapes the checker understands.** Limit postconditions to a small decidable fragment first
   (`result >= e`, `result <= e`, `result == param`, `len(result) == len(param)`). Anything else stays tested only.
   Keeps proving predictable and stops vacuous `ensures true` (lint `vacuous-ensures`).
6. **`reads` / `unchanged` for effectful functions.** Effects already say file.write; add the optional resource
   (`file.write "log/"`) so analysis can check two parallel tasks do not touch the same resource
   (feeds `parallelism-T-0935.md`).
7. **Size limits.** Enforced by lint (error in `--strict`): max parameters (say 6), max body lines (say 40), max nesting,
   max call depth to a leaf. Small functions make interval analysis and model reading local. Cheap, no language change.
8. **`cost` / `allocates` class.** `constant`, `linear`, `unbounded` declared and checked structurally (no recursion,
   list builtins have known classes). Useful for engine code. Medium effort, medium value; defer.
9. **Determinism marker.** `pure` already implies it, but effects such as `time.read`, `random` would be named effects so
   a function without them is reproducible. Needed for `check-determinism` style testing of user programs.
10. **`inline`/`private` visibility.** Today every definition is visible everywhere. A `uses`-only-by line
    (`private to area`) lets analysis treat the function as a part of its caller and skip contract checks at
    internal call sites. Low value while there are no modules.

## Not recommended

- Pointer/ownership annotations, lifetimes, `unsafe` escape hatches: no aliasing or mutation exists, so they add nothing.
- Quantified invariants (`forall`): need an SMT solver, see T-0950.
- Restraints that change runtime behaviour: analysis should be removable without changing program meaning.

## Recommendation

Do in this order: (7) size lints now (small, no syntax), then T-0950 steps 2 and 3, then (1) `cannot-fail` on top, then (5) and (4).
Defer (6) until the parallelism ticket lands; skip (8)-(10) for now.
No code was changed by this ticket.

## What `decreases` means (worked example)

AIP has no loops, so repetition is recursion. A recursive function can run forever (`fn f(n Int) -> Int` calling `f(n)`).
`decreases` is a header line naming a number that gets smaller on every recursive call and cannot go below a floor,
which proves the recursion must stop.

    fn sumTo(n Int 0..) -> Int
      "Add 1 + 2 + ... + n."
      pure
      decreases n
    body
      if n == 0 then 0 else n + sumTo(n - 1)

The checker looks at each recursive call and asks two things:
1. Is the `decreases` value strictly smaller in the call? Here `n - 1 < n`, true whenever the call is reached.
2. Is it bounded below? Here `n` has range `0..`, and the call is only reached when `n != 0`, so `n - 1 >= 0`.

If both hold, every chain of calls is finite, so the function terminates. If the call were `sumTo(n + 1)` or `sumTo(n)`,
the checker would report that `decreases n` does not shrink.

Notes:
- Usually the value is an Int parameter, or the length of a List/Text (`decreases len(items)` for a function that recurses on the tail).
- Mutual recursion (`a` calls `b` calls `a`) needs a `decreases` on each function, comparable across the cycle.
- Without `decreases` nothing changes: the function still works, the heuristic `unbounded-recursion` lint still applies, and it just cannot be marked `cannot-fail`.
- It is a proof of termination only, not of the answer being right; that is what `ensures` is for.
