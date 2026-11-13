# Design decision: memory management once AIP compiles itself (T-0961)

Question: when we use our own compiler (no C#/.NET runtime underneath), what does memory management look like?

**Today.** Generated code is managed C#, so the .NET GC does everything. The language has no pointers, `unsafe` or manual memory
(`security-T-0936.md`).

**Why a simple scheme is enough.** All values are immutable (`global-state-T-0925.md`) and there are no globals, so a value can only
point at values created before it. The heap is therefore acyclic and nothing is ever mutated after creation.
Tasks (`parallelism-T-0935.md`) share nothing. This removes the hard parts of GC: no cycle collector, no write barriers, no
stop-the-world coordination between tasks.

**Can two structs passed in point at each other? No.** A struct is built by a constructor call (`Point(1, 2)`) whose fields must
already be values. For A to hold B, B must exist first; for B to hold A, A must exist first. Both cannot be true, and since a value
is never changed after it is built, there is no way to patch the link in afterwards. Passing two structs into a function does not
change this: the function can only build *new* structs that hold the old ones, so the pointers always go from newer to older.
Data that is truly circular (a graph, parent/child) is written with keys instead, for example a `Map` from id to node, where a node
stores the other's id. An id is plain data, not a pointer, so reference counting never sees a cycle.
**But cyclic data is common in real programs** (doubly linked lists, parent pointers in trees, graphs, observers, a compiler's
symbol table pointing at scopes that point back). The language does not forbid the *shape*, it forbids the *pointer*. The cost is real:
- The program carries an id table and looks nodes up (O(1) or O(log n) per hop instead of a pointer follow).
- "Changing" a node means making a new node and a new table entry; with parent ids, the children need no rebuild, which is the usual
  upside over pointers.
- Dangling ids are possible (an id with no entry), so the lookup returns a `Result`/`Option` or crashes, as `mapGet` does today.
Options, in order of preference:
1. Keep ids and a `Map` (works today, no runtime cost to the memory design).
2. Add a std "arena/graph" type: nodes are created into an arena and referred to by typed handles. Still immutable from the outside,
   still no pointers in the language, and the runtime can free the whole arena at once. This makes the common case ergonomic.
3. Allow real cycles (mutation or recursive `let`) and add a cycle collector or tracing GC. Largest runtime, and it weakens the
   "pure functions cannot race" argument in `parallelism-T-0935.md`. Not recommended unless 1 and 2 prove too slow or awkward
   (measure on the self-hosted compiler first).

This guarantee holds only while we do not add: mutation, lazy or recursive `let` (`let a = S(b)` and `let b = S(a)`), or a
cell/reference type. Any such feature needs a cycle collector, so it must be checked against this note first.

**Recommendation.**
1. **Own runtime, not user-visible.** Allocation and release live in the runtime folder planned in `bootstrap-gaps-T-0954.md`,
   written in AIP plus a few raw-memory intrinsics that only that folder may use. Users still never see a pointer.
2. **Phase 1: allocate and never free** (bump allocator, one region per process). This is enough to bootstrap: the compiler is a short-lived
   batch process. It also gives the smallest first runtime. Add a `--max-memory` limit so runaway programs fail cleanly.
3. **Phase 2: reference counting** for long-running programs. Acyclicity makes it exact. A call that returns a value of fresh
   data and drops its arguments can reuse a uniquely-owned list/text in place (an invisible optimisation, since nobody else can
   observe it), which also fixes the O(n) `listAppend` copy problem noted in the bootstrap plan.
4. **Per-task regions.** Each task gets its own heap and the result is copied out on `taskAwait`, so counts are never shared across
   threads (no atomics) and a task's memory is freed all at once when it ends.
5. **Limits stay visible.** Stack depth and heap size per task are runtime limits, as in `security-T-0936.md`. Running out is a runtime
   crash (`runtime error:`), not something a program can catch.

**Open points.** Measure the compiler's peak allocation under phase 1 before building phase 2. If it fits in a few GB, phase 2
can wait.
