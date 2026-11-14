# What games need that AIP is missing (T-0983)

Checked against `compiler/std/` and the design notes on this branch. Already present and useful for games: `I64`/`F32`/`F64`
and sized ints, bit ops, `List`/`Map`/`Set`/`Queue`/`PriorityQueue` (A*), sorted collections, `Bytes`, `hash`, `tcp`/`udp`,
`file`, `log`, profiling, `native "lib" "symbol"` calls, deterministic emit.

## Blockers (a game cannot run without these)
1. **Time and a frame loop.** No clock, no sleep (`timeNow`, `timeMonotonic`, `timeSleep`, effect `time`). Also loops are
   recursion only: guarantee tail-call elimination (or add a `loop` form) so a game loop that runs for hours cannot overflow the stack.
2. **Float math.** `num` only converts. Need `sqrt pow floor ceil round abs min max sin cos tan atan2 exp log lerp clamp`, `isNaN`,
   `textToFloat`, float formatting.
3. **Random.** Pure seeded generator `randomNext(seed)` (replayable, no effect) plus `randomSeed` with effect `random`.
4. **Input.** Only blocking `readLine`. Need non-blocking key/mouse/gamepad state per frame (effect `input.read`), and raw terminal mode.
5. **Window, rendering, audio.** Nothing. Fastest route: a std `window`/`draw`/`audio` family backed by SDL or raylib through the
   existing native mechanism, effects `window`, `graphics`, `audio`. Needs item 6 first.
6. **Richer native interop.** Native types are only `I64`/`Bool`/`Text`. Graphics libraries need an opaque handle type,
   `Bytes`/pixel buffers passed by pointer, `Float`, small structs by value and callbacks (function pointers).

## Performance (needed for anything beyond a toy)
7. **Mutable bulk storage.** Every collection change copies. Per-frame updates of thousands of entities need a mutable
   `Array<T>`/`Grid<T>` (fixed size, 2D index) under the existing `mutate` effect, or a proven in-place update when the old value is dead.
8. **Value types for math.** Std structs `Vec2 Vec3 Vec4 Mat4 Rect Color Quat` and operators on them, passed without heap allocation.
9. **Parallelism.** Decided in `parallelism-T-0935.md`; needed for job systems (physics, pathfinding, asset loading).

## Language and std gaps that games hit quickly
10. `Option`/`Result` (already decided, open question 5) so lookups and file loads do not crash.
11. Function pointers with `listSort`/`listMap`/`listFilter`/`listFold` (sort by depth, filter entities).
12. Files for assets and saves: `fileExists`, `fileAppend`, `dirList`, `pathJoin`, a JSON (or similar) parse/write, image/sound/font decoding.
13. Text: `textReverse`, char class helpers (`textIsDigit`...), formatting numbers with width and decimals (HUDs).
14. Spatial helpers: `sortedMap` range queries, a grid/quadtree in std (collision, culling).
15. Process: `processExit`, `envGet`.

## Not needed (fits the design)
Scripting/`eval`, reflection, inheritance: an entity-component layout is plain structs + arrays + variants (`match` for state machines).
Determinism helps games: with seeded random and a fixed time step, replays and networked lockstep come almost free.

## Recommended order
1. Time + float math + seeded random + tail-call guarantee (small, pure or single-effect; enables a text-mode game, e.g. snake).
2. Mutable `Array`/`Grid` and `Vec2` struct (performance and ergonomics).
3. Non-blocking input and `Option`/`Result`.
4. Native interop upgrade (handles, bytes, float, callbacks), then an SDL/raylib-backed `window`/`draw`/`audio` std family.
5. Function-pointer list helpers, parallel jobs, asset/file helpers.
Milestone: `examples/snake` (terminal, frame loop, random, input), then `examples/breakout` (window, sprites, audio).

## Status (iteration 2)

Items 1-3 (time/sleep and tail calls, float math and textToFloat, random) are implemented; see game-basics-T-0983.md.
