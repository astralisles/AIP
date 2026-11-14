# Choosing a collection kind (T-0951)

Question: can a program say tree vs hash map, linked list vs array list, and what else?

## Decision: the type names the guarantee, not the data structure

An AI picks a kind by what it needs to observe (order, cost model), not by a pointer layout. All AIP values are
immutable, so a "linked list" and an "array list" cannot be told apart except by speed; the language therefore does
not offer both as separate types. A kind exists only when it changes what a program can see or what it promises.

| Need | Type | Status |
|---|---|---|
| Lookup by key, keep insertion order | `Map<K,V>`, `Set<T>` (hash) | done |
| Lookup by key, keys always in ascending order (tree) | `SortedMap<K,V>`, `SortedSet<T>` | done (T-0951): I64 or Text keys |
| Indexed sequence | `List<T>` | done |
| First-in-first-out / double ended | `Queue<T>`, `Deque<T>` | proposed: `queuePush`, `queuePop` return a new queue; amortized O(1) |
| Always take the smallest item | `Heap<T>` (priority queue) | proposed |
| Count duplicates | `Bag<T>` (Map<T,I64> wrapper) | proposed; can be plain std functions |
| Dense I64 flags | `Bits` | proposed, needs the numbers decision (open question 9) |
| Keys of structs/variants in a sorted kind | needs an `order` rule for user types | open |

Linked vs array list: not a type. The compiler (back end) may use whichever representation it measures as faster
(`aip profile` gives the data). If a program needs cheap prepend or queue behaviour it asks for `Queue`/`Deque`, which states
the intent. Same for hash vs tree: use `Map` when order does not matter and `SortedMap` when it does; `SortedMap` costs
O(log n) per change and may not be replaced by a hash by the back end, because its order is observable.

## What T-0951 implemented
`SortedMap<K,V>` and `SortedSet<T>` with std groups `sortedMap/` and `sortedSet/` (`sortedMapSet`, `sortedMapGet`, `sortedMapHas`,
`sortedMapRemove`, `sortedMapSize`, `sortedMapKeys`, and the `sortedSet*` equivalents). Backed by a C# `SortedDictionary` with an ordinal
comparer; every change copies, like `Map`. Example: `examples/sorted`.
Known limit: keys must be I64 or Text. The checker does not yet reject other key types (a struct key would crash at run time);
rejecting them is the first follow-up.
