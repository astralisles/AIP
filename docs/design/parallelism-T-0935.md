# Design decision: parallelism (T-0935)

> `taskRun`/`taskAwait` implemented in T-0978: see `fn-and-tasks-implemented-T-0978.md`.

Yes, but only as deterministic, effect-visible parallelism, and not yet. Because values are
immutable and there is no global state (T-0925), pure functions cannot race, so parallelism
needs no locks, atomics or shared memory in the language.

Shape (builds on `Fn` from T-0928):
- Data parallelism first: std `listMapPar(list, &f)`, `listFoldPar(list, &f, &combine)`.
  `f` must be pure (no effects in its `Fn` type); `combine` must be associative. The result
  is identical to the sequential `listMap` / `listFold`, always in input order, so
  `example`/`ensures` checking and "stage 2 output == stage 1 output" still hold.
  The back end chooses the thread count; it is never visible to the program.
- Parallel effects (e.g. compile many files, each reading and writing files): `taskRun(&f, arg)`
  returns a `Task<R>`; `taskAwait(t)` returns the result. The effects of `f` must appear in the
  caller's `effects` line plus a `parallel` effect, so the index shows which functions fork.
  Tasks share nothing: arguments are copied/immutable and the only way back is `taskAwait`.
  Results are consumed in a program-chosen order, so output order stays deterministic.
- No shared mutable state, no locks, no channels, no `async`/`await` coloring. Any
  cross-task communication needs the explicit state handle from T-0925, not planned now.
- Errors: a failing task surfaces at its `taskAwait`; unawaited tasks are a compile error.
- Back end: C# `Parallel`/`Task` (the main thread already runs on a 512 MB stack thread;
  task threads need the same stack size for deep recursion); other back ends may run sequentially,
  which must give the same result.

Not implemented. Needs `Fn` first. Immediate use: compiling the files of a project in the
bootstrapped compiler (parse each `.fn` in parallel, check and emit in order).
