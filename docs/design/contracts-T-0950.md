# Function contracts and static verification (T-0950)

Question: should we define function contracts, use them statically to verify code, and would it help?

## Where we are

Contracts already exist: `requires` (precondition), `ensures` (postcondition), `example`, and Int/Text ranges
(`Int 0..100`). They are type-checked and **run** by `aip test` and by the interpreter on every call
(`tests/contracts`). They are **not proved**; the index says "checked, not proved". `aip lint` adds a few
syntactic checks (division-by-zero literal, unbounded recursion, ...).

So the real question is how far to go from "tested" to "proved".

## Answer: yes, keep contracts; prove in small steps, no SMT solver yet

Contracts help this language more than most because it was built for it: functions are pure unless they
declare effects, there is no mutation, no aliasing, no loops, no globals. Those are exactly the things that make
verification hard elsewhere. A contract is also the best thing a model can read in the index: it states what a
function needs and gives without reading the body.

Benefits:
1. Bugs found at compile time instead of a crash (`clamp(1, 10, 0)`, `x / 0`, `listGet` out of range).
2. The index becomes a trustworthy spec: "proved" vs "tested" per function.
3. Models writing code get precise, local errors ("argument 2 may be 0, requires divisor != 0").
4. Fits T-0944: a failed `requires` is the only crash left, so proving them removes crashes.

Costs and risks:
- A prover that says "cannot prove" on correct code annoys people; a model will "fix" it by weakening the contract.
  So unproved must never be an error, only a warning (or the existing "tested" status).
- Models can write wrong or vacuous contracts (`ensures true`). A proof of a wrong contract is still wrong.
- SMT (Z3) would add a native dependency, unpredictable run time, and clash with the bootstrapping goal
  (the compiler should one day be written in AIP; see `bootstrapping.md`).
- Recursion needs a `decreases` clause to prove termination; without it we only prove partial correctness.

## Plan (each step is useful alone, in order)

1. **Constant call-site check (cheap, do first).** If all arguments of a call to a pure function are literals/constants,
   evaluate `requires` and ranges with the existing interpreter at check time. Failure is the warning
   `requires-always-false`. Needs no new theory.
2. **Facts from conditions (own small analysis, no dependency).** Interval + simple comparison facts for Int,
   propagated through `let`, `if c then ... else ...` (inside `then`, `c` is known) and `match`. Used to discharge
   `requires`, ranges, `/` and `%` by zero, and `ensures` of the form `result >= 0` / `result <= n`.
   Output per call site: proved / unproved (warning) / disproved (error). The index shows "proved" only when all
   its contracts and all callee `requires` at its call sites are proved.
3. **Termination.** Optional `decreases expr` line; check it strictly shrinks on each recursive call. Replaces the
   heuristic `unbounded-recursion` lint where present.
4. **Optional `aip prove` using an SMT solver**, a separate command, never part of `check`/`build`, only if steps 1-3
   show real programs need more than intervals. Decide then.

Not planned: invariants/quantifiers (`forall`) in contracts, contracts on effectful functions beyond their effects
list (effects already are a checked contract), and any contract that changes runtime behaviour.

## Rules for contract authors (people and models)

- Prefer ranges on types for value limits, `requires` for relations between parameters, `ensures` for the result.
- A contract states what callers can rely on; do not restate the body.
- Keep a failing `example` for every contract: examples remain the always-available tested fallback.

## Recommendation

Adopt steps 1 and 2 as the next tickets (about 300-500 lines in `Lint.cs`/a new `Prove.cs`, tests in
`tests/lint` and `tests/contracts`). Defer 3 and 4. Nothing in the language syntax needs to change for 1 and 2.
