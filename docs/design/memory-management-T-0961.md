# Design decision: memory management once AIP compiles itself (T-0961)

Question: when we use our own compiler (no C#/.NET runtime underneath), what does memory management look like?

**Today.** Generated code is managed C#, so the .NET GC does everything. The language has no pointers, `unsafe` or manual memory
(`security-T-0936.md`).

**Why a simple scheme is enough.** All values are immutable (`global-state-T-0925.md`) and there are no globals, so a value can only
point at values created before it. The heap is therefore acyclic and nothing is ever mutated after creation.
Tasks (`parallelism-T-0935.md`) share nothing. This removes the hard parts of GC: no cycle collector, no write barriers, no
stop-the-world coordination between tasks.

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
