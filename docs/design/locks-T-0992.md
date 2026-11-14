# Design decision: locks (T-0992)

**Answer: no `lock`/`unlock` functions. Offer a shared cell, `Shared<T>`, with scoped access. For many readers and few
writers, readers take no lock at all.** Not implemented; it needs `Fn` (T-0928) and tasks (T-0935) first.

## Why not a classic lock
`lock(m)` / `unlock(m)` as two calls can be forgotten, taken in the wrong order (deadlock) or held across a blocking
call. It also protects data the lock does not name. AIP has no global state (T-0925) and values are immutable
(T-0935), so the only thing that needs protecting is a *cell holding an immutable value* that several tasks share.
Make the cell and the lock one thing.

## Shape
```
sharedNew(value T) -> Shared<T>                              effects: shared.write
sharedRead(cell Shared<T>) -> T                              effects: shared.read
sharedUpdate(cell Shared<T>, f Fn(T) -> T) -> T              effects: shared.write   (returns the new value)
```
- `f` in `sharedUpdate` must be pure (no effects in its `Fn` type). The "critical section" is therefore always a
  pure function: it cannot take another lock, block, or call `taskAwait`, so **deadlock is impossible by construction**
  and the lock cannot be forgotten or left held.
- `sharedRead` returns an immutable snapshot. Later updates never change a snapshot someone already holds.
- A program that touches a `Shared<T>` shows `shared.read` / `shared.write` in the index, so the places where results
  can depend on timing are visible and grep-able. Passing a `Shared<T>` to `taskRun` is allowed only with these effects
  listed in the caller (see T-0935: tasks otherwise share nothing).
- Need "update and also return something else"? Make `T` a record and read the result from the returned value.

## Many readers, few writers
Because the stored value is immutable, the back end does not need a read/write lock:
- **Read:** one volatile read of the current reference (C# `Volatile.Read`). Wait-free, never blocked by a writer,
  never blocks a writer.
- **Write:** compute `f(old)` and publish with compare-and-swap (`Interlocked.CompareExchange`); if another writer won,
  rerun `f` on the new value. Re-running is safe only because `f` is pure, which is why that rule exists. A plain
  `lock` around the update is an equally valid back end (simpler, no retries; use it if `f` is expensive and
  writers collide often). The program cannot tell the difference.
- This beats `ReaderWriterLockSlim`: no writer starvation, no reader/writer upgrade deadlocks.

Costs and limits:
- Each update builds a new value. For a large list/map that is a full copy unless the collections become persistent
  (structural sharing, see `collection-kinds-T-0951.md`). Until then, shard: keep a list of small cells keyed by hash
  and update only one shard.
- Writers are ordered by timing, so output that depends on the order of writes is not reproducible. Reads of a value
  nobody writes anymore are. Use `taskAwait` and ordered results (T-0935) when determinism matters.
- A reader can see a stale value (snapshot semantics). If a decision must be atomic with the write (check-then-set),
  do it inside `sharedUpdate`'s `f`, never as `sharedRead` followed by `sharedUpdate`.

## Waiting for a condition
No condition variables. Waiting for "something changed" is `taskAwait` (finished work) or a queue
(`queues-T-0952.md`); polling with `timeSleep` (T-0981) is only for backoff.

## Out of scope
Locks on outside resources (file locks, cross-process mutexes) are a separate question: they reach outside the program,
so they would be their own effect (for example `file.lock`) with the same scoped form
`fileWithLock(path, &f)`. Not planned now.

## When to build
When the first real need appears (the bootstrapped compiler sharing a symbol table across parallel parse tasks is the
likely one). Files then: `compiler/std/shared/*.fn` (one per function), a `Shared<T>` line in `GenericTypes.cs`,
an `Rt` part in `CsBackend.Runtime.cs`, and a `tests/checks/shared.ps1` that runs many reader tasks and a few
writer tasks and checks the final value equals the sequential result of the same updates (commutative `f`, such as
add 1).
