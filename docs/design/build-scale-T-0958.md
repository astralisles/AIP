# Design decision: compiling OS / AAA-game scale software quickly (T-0958)

Target: 1,000,000+ functions (one function per file), a full build in minutes, an edit-and-run in about a second.

## Where we are (measured, see compiler/README.md)
- Front end (read, parse, check) is parallel and cached: 200,000 functions answer a query in 3.7 s after one edit.
- Back end is the bottleneck: `CsBackend` emits one `.cs` per function into ONE csproj and runs `dotnet publish`
  (about 2.5 s for a tiny program). One assembly means one Roslyn compile that grows with the whole program, and
  any change recompiles all of it. Output is skipped only if ALL generated C# is byte-identical.
- Build is deterministic (`check-determinism.ps1`), which is the precondition for everything below.

## Why AIP can scale better than C/C++
No headers, macros, includes, templates-by-text or global state. A function's whole interface is its signature
line (types, ranges, effects), so "does a change affect callers?" is answered exactly, not by textual inclusion.

## Plan, in order of payoff
1. **Interface hashing (cheap, do first).** Hash each function's signature+effects (its "interface") and its body
   separately. If only a body changed, only that function is re-emitted and re-compiled; callers are untouched.
   If the signature changed, rebuild exactly the callers (`aip query callers` already has this graph). Persist
   hashes in `build/`. Put the stamp per unit, not per project as today.
2. **Split into many assemblies (units).** Group functions into units (a folder = a unit; the folder tree is the
   module tree) and compile each to its own DLL, depending only on the interface of the units it calls. Changing a
   body recompiles one unit; units compile in parallel on all cores. Keep units around 500-5,000 functions.
3. **Content-addressed build cache.** Key = hash(unit sources + interface hashes of dependencies + compiler version +
   target). Store outputs in a shared cache (local disk, then a remote one for CI and teammates). Needs determinism,
   which we already test. A clean checkout then costs mostly cache downloads.
4. **Skip the .NET toolchain per edit.** Call Roslyn in-process from a long-running `aip serve` (also planned in the
   README for queries): keeps the checked program and compilation state in memory, re-checks only changed files,
   no `dotnet publish` process start (about 2.5 s saved each time). Use `aip test`/the interpreter for the inner loop.
5. **Native back end for shipping (OS/game).** C# on .NET is fine for tools, not for kernels or frame loops. Add a
   back end that emits LLVM IR / C per unit (same unit and cache design), with release builds optimising per unit and
   a final link. Debug builds use the fast path (no optimisation). Effects in signatures let the optimiser treat
   `pure` functions as freely reorderable, inlinable and parallelisable.
6. **Parallelism inside the compiler.** Check and emit units in parallel (check already is). Uses `listMapPar` /
   `taskRun` from `parallelism-T-0935.md` when the compiler is self-hosted (`bootstrapping.md`).
7. **Distribute.** Units are independent jobs with hashed inputs, so a build farm can run them (same protocol as the
   cache). Only after 1-4.
8. **Keep it fast: gates.** Extend `scripts/bench-query.py` into a build benchmark (generated projects of 1k to 1M
   functions: clean build, no-op build, one body edit, one signature edit) and fail `scripts\test.ps1` when a
   budget regresses. Without a budget, build time only ever grows.

## Targets to hold ourselves to
| Case | Goal at 1M functions |
|---|---|
| No-op build | under 2 s (hash check only) |
| Edit one body | under 3 s, recompiles 1 unit |
| Edit one signature | under 10 s plus callers' units |
| Clean build, cold cache | minutes on a many-core PC; seconds with a warm shared cache |

## Risks
- Generics and type-erased lists (`generics-T-0949.md`): an instantiation lives in the unit that uses it; the cache key
  must include type arguments, or use shared erased code.
- Cyclic calls between units: require the unit graph to be acyclic (recursion allowed inside a unit), or merge cycles.
- Whole-program optimisation (inlining across units) trades against incrementality: allow it in release only.

Nothing here is implemented yet; step 1 is the next ticket.
