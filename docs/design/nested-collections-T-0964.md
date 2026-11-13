# Arrays of bytes and collections of collections (T-0964)

Question: can we have arrays such as arrays of bytes, and collections of collections?

## Answer
- **Arrays**: `List<T>` is the array. It is indexed (`listGet`, `listSet`) and immutable. There is no separate array type
  (see `collection-kinds-T-0951.md`).
- **Bytes**: `List<U8>` works (`numToU8` / `numFromU8`). The `bytes*` and `hash*` std functions use `List<Int>` with each
  item 0..255, because `List<I8>` is not a `List<Int>` (no widening inside generics, `data-types-T-0945.md`).
  `bytesFromText`, `bytesToText`, `bytesToHex`, `bytesFromHex`, `hashSha256`, `hashHmacSha256`; an item outside 0..255 is a run-time error.
  A dedicated packed `Bytes` type stays proposed (`bootstrap-gaps-T-0954.md`); it is needed only for memory/speed, not for meaning.
- **Collections of collections**: any nesting type-checks and runs: `List<List<Int>>`, `Map<Text, List<Int>>`,
  `List<Set<Int>>`, `Map<Int, Map<Text, Int>>`. Example and test: `examples/nested`.

## Limits
- `Set<List<..>>` and keys that are collections compare by reference, not by contents (a distinct list with equal items is not found).
  Use `Int` or `Text` keys. Same family as the open sorted-key-type follow-up in T-0951.
- `{..}` in a text cannot contain a text literal (`"{f("x")}"`): put the call in a `let` first.
- No literal for a list; build with `listAppend`.

## Housekeeping done in this ticket
- Removed four stub files from the abandoned T-0962 run (`std/compress/*`, `std/crypto/*`) that broke the `std-layout` check
  and so every program.
- Implemented the already declared but missing externs `bytes*` and `hash*` (back end + interpreter).
