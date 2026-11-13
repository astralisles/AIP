# Open questions and answers (T-0944)

Decided so far: global state, function pointers, security, parallelism, profiling, std layout. Gaps left, ordered by
how much they hurt if answered late. Items marked DONE were answered in iteration 2; the rest are still questions.

1. **Does an LLM write AIP better than it writes Python?** Nothing is measured yet. Build a 20-task benchmark (first-try
   compile rate, tests passed, tokens, edit success). Recommended next. (`examples/` + `tests/errors` = starting corpus.)
2. **Are compiler errors good enough for a model to fix its own mistake?** Want stable codes and a `--json` mode; measure
   one-retry fix rate. (Errors already print `error <code> <file>:<line>` plus `fix:`.)
3. **DONE: how does a model learn a language it never saw?** `aip spec` prints `compiler/spec.md`, a one-page spec
   (about 25 lines) meant to be the first thing in a prompt. Rule: it must stay one page; if a feature cannot be explained
   in a line or two, simplify the feature. A test fails if the spec passes 40 lines.
4. **DONE: the index at 50,000 functions.** `aip find <projectDir> <word>...` searches project and std functions by name,
   doc and effect and prints the best 10 (`project|std | signature | effects | doc`). A model reads the spec, then finds
   instead of reading the whole index. Next steps if needed: a per-folder summary line in `index`, and fuzzy matching.
5. **DONE (design): errors and absence.** Not implemented. Decision: no exceptions and no null. A function that can fail
   returns `Result<T, E>`; one that may have no answer returns `Option<T>`; both are ordinary `variant`s used with `match`
   (every case required), so forgetting a failure is a compile error. The failure type shows in the signature, so the
   index shows how a function can fail, like effects. Runtime crashes stay only for bugs the type system cannot express
   (out of memory, stack, a broken `requires`), as in T-0936. `ensures`/`requires` failures become a `Result` error value
   when the caller opts in. Standard functions that can fail today (`fileRead`, `mapGet`, `listGet`) get `Result`/`Option`
   versions when it lands. Needs generic variants first.
6. **How do people (and models) depend on other people's code?** "No imports" means no libraries; decide copy-in folder,
   versioned package, or std only; needs a supply-chain answer (T-0936).
7. **How does the language change without breaking old programs?** A version line per project and a change rule.
8. **Testing.** `example` lines are parsed but never run. Running them is the cheapest big win.
9. **Which numbers?** Only `I64`. Overflow, `Float`, decimals, `Bytes`, Unicode text all undecided.
10. **Outside-world effects.** Network, time, randomness, environment: names, scopes, deterministic replay.
11. **Is self-hosting still the right goal?** It costs time that could go to 1, 2 and 8.
12. **DONE (decision): who is the user?** The AI. AIP is written, read, fixed and searched by a model; a human is a
    reviewer, not the audience. Where they conflict, the model wins: choose the form that is least ambiguous and cheapest in
    tokens to read and to repair, even if it looks unfamiliar or verbose to a human (explicit effects lines, one definition
    per file, full-word names, uniform shapes, machine-readable output, no clever syntax or shortcuts with several
    spellings). Consequences: new commands print stable plain text (and `--json` where useful); error messages carry a
    `fix:`; the spec stays one page. Human aids (`aip explain`, pretty output) are optional extras, never the reason for a
    language rule. This is the tie-breaker for all later design tickets.
13. **What does "done" mean for the prototype?** Suggest: a model builds a 500-line tool from `aip spec` alone with no
    human edits.

Recommended next three: 1 (benchmark; it now also tests whether the spec and `find` work), 8 (run `example` lines),
implementing 5 (`Option`/`Result`, after generic variants).
