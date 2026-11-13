# `Bytes` removed: bytes are `List<U8>` (T-0984)

Question: where is `Bytes` defined, and shouldn't it be a composite of existing types? Decision: yes (option 3).

- The built-in `Bytes` type is gone (it was in `Checker.KnownTypes` and mapped to C# `byte[]`).
- A byte array is `List<U8>`. `fileRead`, `fileWrite`, `tcpSend/Receive`, `udpSend/Receive`, `bytesEncode`, `bytesDecode`,
  `bytesFromList` use it. The C# back end converts to `byte[]` only at the extern call (`Rt.ToByteArray` / `Rt.FromByteArray`).
- Removed `bytesEmpty`, `bytesLength`, `bytesGet`, `bytesSlice`, `bytesJoin`: use `listEmpty`, `listLength`,
  `numFromU8(listGet(..))`, `listSlice`, `listConcat`.
- Cost: a byte list is a persistent list of boxed bytes, so large files use more memory and I/O copies. If that matters later,
  pack `List<U8>` inside the back end without changing the language (the earlier option 2).
- The older `bytesFromText`/`bytesToText`/`bytesToHex`/`bytesFromHex` still use `List<Int>` (see `nested-collections-T-0964.md`).
