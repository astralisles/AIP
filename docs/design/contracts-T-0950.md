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

1. **Constant call-site check (cheap, do first). DONE in T-0950 #3 (`aip lint`, `Interp.FirstBrokenRequires`).** If all arguments of a call to a pure function are literals/constants,
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

## What it looks like

Contract syntax does not change. A function with all three kinds of contract:

    fn safeDiv(a Int, b Int 1..) -> Int
      "Divide a by b; b must be at least 1."
      pure
      requires a >= 0
      ensures result <= a
      example safeDiv(10, 2) == 5
    body
      a / b

**Today (real output)** for a caller `safeDiv(10, 0)`: `aip check` says `ok`, `aip lint` warns
`main.fn:5: warning literal-out-of-range: argument `b` of `safeDiv` is 0, outside 1..`, `aip test` runs the one example.
Anything not a literal only fails when the program runs.

**After step 1 (implemented; real wording)**

    main.fn:5: warning requires-always-false: call `f(10, 1)` breaks `requires` of `f` (f.fn:4) and always fails at run time

**After step 2 (proposed wording, not implemented)**

    main.fn:9: warning requires-unproved: safeDiv(x, y) needs `y >= 1`; known: y >= 0
                hint: call it inside `if y >= 1 then ...`
    main.fn:5: error requires-disproved: safeDiv(10, n) with n = 0 here (inside `if n == 0`)

The index line gains a status instead of "checked, not proved":

    safeDiv(a Int, b Int 1..) -> Int | pure | requires a >= 0 | ensures result <= a | proved
    average(a Int, b Int) -> Int     | pure | example x2                              | tested

Unproved never blocks `build`; only a disproved contract (a call that must fail) is an error.
