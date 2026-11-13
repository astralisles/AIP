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

**What really happens to a struct (worked example).** Take `struct Health` with `hp Int` and `lastHit Int` (same shape as
`examples/shapes/Point.struct`). `let h = Health(10, 3)` makes one block of memory: a small header (size, reference count) followed by
the two fields side by side, here `10` and `3`. `h` is just the address of that block. `h.hp` is a `.field` read: the compiler knows
`hp` is field 0 of `Health`, so it loads the value at (address + header + 0). `h.lastHit` loads field 1. Nothing is searched by name
at run time. Small values like `Int` are stored inside the block; bigger ones (`Text`, `List`, another struct) are stored as the
address of their own block, which is the only kind of pointer there is.
"Taking damage" does not edit the block. `Health(h.hp - 2, 5)` allocates a second block, and the old one is freed when nothing
refers to it any more (phase 1: never; phase 2: its count reaches 0). If only one reference to the old block exists, the runtime may
reuse it in place, which nobody can observe. Passing `h` to a function copies the address, not the fields.

**Does it scale? A server with a player and a fish that point at each other.**
Written with ids, the data is: `Player(id, luredFishId: Option<Int>, ...)`, `Fish(id, targetId: Option<Int>, ...)`, and a world
`World(players: Map<Int, Player>, fish: Map<Int, Fish>)`. Neither struct holds the other, only the other's id, so there is no cycle
and no problem for memory management. This is the same layout most game engines use (entity ids plus component tables), so the
*shape* scales. What decides whether the *speed* scales:
- **Updates must not copy the collection.** Today `listAppend` and `mapSet` copy everything (`bootstrap-gaps-T-0954.md`). With 100,000
  fish, moving one fish per tick would copy 100,000 entries. A server needs persistent collections (a tree or hash trie) where an
  update builds only the changed path (about log n nodes, ~17 for 100,000) and shares the rest with the old version. This is a back-end
  change, not a language change, but for a server it is required, not optional.
- **Cost per hop.** `world.players` -> lookup -> `luredFishId` -> lookup is two tree/hash lookups instead of two pointer follows,
  and it touches more cache lines. Expect a constant factor slower than pointers, not a different big-O.
- **Garbage per tick.** Each tick allocates new path nodes and new structs for everything that changed, and frees the old ones.
  Reference counting makes that steady and predictable (no long GC pauses), but it is real allocator work. Keep a tick's changes
  small (change only what moved) and measure.
- **Dangling ids.** If the fish despawns, `player.luredFishId` still holds its id. The lookup returns `Option`, so the code must handle
  "gone"; this is a bug class pointers do not have (they have use-after-free instead, which AIP rules out).
- **Parallelism.** Split the world into regions (each its own `Map`), update regions as tasks (`parallelism-T-0935.md`), then merge
  results in a fixed order. Tasks share nothing, so no locks. Two things that point across regions use ids, resolved at merge.
- **Where it would not scale:** a very hot single object that thousands of entities all update each tick (one global counter or
  one huge shared struct). That serialises through one value; split it (per-region counters, summed at merge).
What is not yet shown: no measurement exists. Before committing, build one benchmark (100k fish, 10k players, 1 tick) on the
current C# back end with persistent maps, and compare to a mutable C# version.
If the numbers are bad, the escape hatch is a *store*: an effect-gated mutable table (for example effect `store`) shown in the
signature and the index, so the mutation stays visible and out of pure code. That breaks the "no mutation" rule in `compiler.md`,
so it is a decision for the owner, not something to add quietly.

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
