# Where is `Bytes` defined, and should it be a composite? (T-0984)

## Where it is defined
`Bytes` is a built-in primitive, not a composite and not a `.type` file:
- Checker: `KnownTypes` in `compiler/Aip.Compiler/Checker.cs` (next to `Int`, `Text`, `TcpConn`, ...).
- C# back end: maps to `byte[]` in `CsBackend.cs` (type mapping).
- Std functions: `compiler/std/bytes/*.fn` (header-only `extern`, e.g. `bytesJoin`); implementations in `CsBackend.Externs.Net.cs`
  and `Interp.Externs.Net.cs`.
- Origin: T-0959 (`network-T-0959.md`); the packed type was proposed in `bootstrap-gaps-T-0954.md`.

## Should it be a composite?
Meaning-wise yes: `List<U8>` already expresses a byte array (`nested-collections-T-0964.md`). `Bytes` exists only for:
1. Memory/speed: `byte[]` is 1 byte per item; `List<U8>` is a boxed/wide list.
2. Interop: network and file externs take and return `byte[]` directly, with no conversion copy.
3. `List<I8>` is not a `List<Int>` (no widening inside generics), so the older `bytes*`/`hash*` functions use `List<Int>` 0..255.

## Options
1. Keep `Bytes` as a built-in primitive (current). Cheapest, fastest, but a second array kind.
2. Make `Bytes` an alias of `List<U8>` and let the back end pack that list as `byte[]`. One array concept, no new type;
   needs an alias feature plus a packing special case in the back end, and `bytesJoin` etc. change signature to `List<U8>`.
3. Remove `Bytes`; use `List<U8>` everywhere. Simplest language, slowest, and network/file externs copy.

Recommendation: option 2 if the language should have only one array kind; otherwise keep 1. No code was changed.
