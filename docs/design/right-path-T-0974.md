# Are we on the right path? (T-0974)

## Answer

**The idea is right; the order of work has drifted.** A model that has never seen AIP writes correct programs
from the one-page spec and the compiler's messages alone (measured below, for the first time). But most recent
tickets went to breadth (std families, sockets, native libraries, memory management, self-hosting) while the
question the project exists to answer, open question 1 in `open-questions-T-0944.md`, was never measured, the
test suite on the main line was red without anyone noticing, and the status documents went stale.

## Evidence

### 1. The test suite was red

`scripts\test.ps1` at the start of this ticket: 2 failures (`example aipc: aip lint`, `example scaling: aip lint`),
73 `hidden-failure` warnings. Cause: T-0966 added the rule "a function that calls a `fails` function is marked
`fails`", while T-0961/T-0967/T-0969 added callers on parallel branches. Each branch was green alone; the merge was not,
and nothing ran the suite after merging. Fixed here (44 functions marked `fails` by `scripts\fix-hidden-failure.ps1`).

The suite is also not fully stable: of three full runs at the end of this ticket, one reported six `aip fuzz`
failures with empty output (aipc, control, expr, floats, generics, hello) and two were green; `aip fuzz` on the same
projects alone passes. Cause not found; it looks like the parallel fuzz jobs under load, not a compiler fault.

### 2. First measurement: can a model write AIP from the spec alone?

Four tasks in `tests/bench` (RPN calculator, word frequency, bank transfers with a variant result, Game of Life).
A fresh model per task got only `aip spec`, `aip find`, `aip check`, `aip run` through `scripts\bench-attempt.ps1`,
which logs every call, and was told not to read anything in the repo. Each program's output was then compared with
`expected-output.txt`.

| Round | Spec | Correct output | Clean on first `check` | Compile errors (total) | `find` calls per task |
|---|---|---|---|---|---|
| 1 | as before this ticket | 4 of 4 | 3 of 4 | 1 (`unknown-name`, fixed from its `fix:` line) | 15, 14, 11, 15 |
| 2 | with the lines added in this ticket | 4 of 4 | 4 of 4 | 0 | 6, 6, 2, 4 |

Limits of this pilot, so it is not over-read: four small tasks, one run per task and round, one model family, the
expected output was given, there is no Python baseline, and "did not read the repo" rests on the model's word (the
log shows only its compiler calls). Round 2 differs from round 1 by the spec and the `find` fix together. It shows
the language is learnable; it does not show it is *better* than Python.

### 3. What the pilot says is wrong with the language

All eight runs reported the same costs, independently:

- **Programs are 2 to 4 times longer than the logic.** 6 to 23 files and 37 to 152 lines for 15 to 40 lines of logic.
  No loops plus one function per file plus single-expression branches means every loop and every branch with two
  steps becomes its own file, with the loop state repeated in each signature.
- **No list literal**: start data is built by recursive functions or chains of `listAppend`.
- **No safe forms**: `mapGet` / `textToInt` crash instead of returning an `Option`; no get-with-default.
- **No list sort, fold, map or each**; no pop/last for a list used as a stack.
- **The spec leaves layout to guesswork**: the `main` signature, where `body` is indented, `match` arm layout,
  operator precedence, whether `if` chains. Every guess was right, which says the syntax is predictable, but they
  are guesses.
- `find` shows only 10 results and matches loosely.

### 4. Self-hosting

`examples/aipc` has the lexer, header parser and body parser (about 100 files), each compared with the C# compiler.
The checker, the emitter and `process.run` are missing, and they are the larger part. Self-hosting proves the
language can carry a big program, which is useful, but it does not test whether models write AIP well, and it
multiplies the cost of every language change (two parsers to keep equal). `bootstrapping.md` and
`open-questions-T-0944.md` are both out of date (items listed as missing or open are done).

## Changed in this ticket

- 44 example functions marked `fails`; the suite is green again. `scripts\fix-hidden-failure.ps1` repeats this.
- `tests/bench` (4 tasks) and `scripts\bench-attempt.ps1`: the benchmark is now repeatable.
- `compiler/spec.md`: statements, multi-statement branches, `noop()`, field-less cases, "no loops, no list
  literal", the std naming pattern and nine names (27 lines; limit 40).
- `aip find`: a quoted multi-word query is split into words (round 1 lost about half its searches to this), and
  "no function matches" now has a `fix:` line. Test: `tests/checks/find-quoted.ps1`.

## Recommendations

1. **Keep the core design** (one function per file, effects in the signature, one-page spec, `fix:` lines). The
   pilot supports it.
2. **Make the benchmark the gate.** Grow `tests/bench` to about 20 tasks, including two of 300 to 500 lines, add a
   Python baseline and a second model, and re-run it after every language ticket. This is open question 1 and the
   definition of done in question 13.
3. **Pause self-hosting after the parser** until 2 exists. Keep the three compare checks running.
4. **Fix the friction the pilot found, in this order**: list literals; value-returning multi-statement branches
   (`let` inside `if`/`match` arms); `Option`-returning `mapGet`/`textToInt` and get-with-default; list sort;
   one iteration form (fold/each or a bounded `for`). Measure each with the benchmark.
5. **Run `scripts\test.ps1` after every merge to the main line**, not only on the branch.
6. **Refresh the status documents** (`open-questions-T-0944.md`, `bootstrapping.md`) and add the missing spec
   items from section 3 (the `main` signature, file layout, precedence).
7. **Stop adding breadth** (new std families, targets, runtime features) until 2 and 4 are done.
