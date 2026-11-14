# Function values and tasks: what is implemented (T-0978)

Implements the first part of T-0928 (function pointers) and T-0935 (tasks), the concurrency step of `async-io-T-0978.md`.

## Function values
- `Fn<A, R>`, `Fn<A, B, R>`, ...: the last type argument is the result.
- `&name` makes a function value; it counts as a call of `name` for reachability.
- Difference from T-0928: effects are **not** part of the type. Only pure functions can be referenced (`fn-effects`).
  A generic function cannot be referenced (`fn-generic`).
- User code cannot call an `Fn` value yet; values are passed to std functions: `listMap`, `listFilter`, `listFold`, `listMapPar`.

## Tasks
- `taskRun(&f, arg)` returns `Task<R>` and has effect `parallel`; `&f` may be effectful here, and its effects are added to the caller.
- `taskAwait(t)` returns the result and rethrows a failure of the task.
- A task must be let-bound, awaited exactly once on every path (`task-unawaited`, `task-double-await`),
  and cannot be a parameter, a result or used anywhere but as the `taskAwait` argument (`task-escape`).
- C# back end: one thread per task (512 MB stack). `listMapPar` splits the list over cores and keeps input order.
- `aip test` (interpreter): `listMapPar` runs in order; `taskRun`/`taskAwait` are refused like other effects.

## Not done
`listFoldPar`, calling `Fn` values from user code, effects in `Fn` types, TCP timeout variants, file handles.
