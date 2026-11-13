# Design decision: function pointers (T-0928)

Yes, but as a restricted feature, and not yet. Higher-order operations (`map`, `filter`,
`fold`, sort by key, callbacks in a compiler pass) are needed for the bootstrapped
compiler; without them every loop is hand-written recursion.

Shape: a function type `Fn<(A, B), R>` (effects part of the type, e.g. `Fn<(Int), Int> !file.read`).
A value of that type can only be a reference to a named top-level function (`&name`). That
keeps the promises of the language:
- No closures, so no captured hidden state (consistent with the no-global-state decision).
- No anonymous lambdas: every callee has a file, a signature and an index line.
- A function taking a `Fn` parameter may only perform the effects its own `effects` line
  lists plus those in the `Fn` type, so effects stay visible in the index.
- Reachability: `&name` counts as a call from the referencing function.
- Back end: C# delegate (`Func<..>`); a back end without delegates may use a lookup table.

Implemented (T-0976), with these limits: `&name` works only for a non-generic, pure, non-extern function (error
`bad-fn-ref` otherwise); `Fn<..>` carries no effects. A function value is a boxed delegate in C# and a closure in the
interpreter, so generic std functions need no per-type code. Fn values cannot be compared or put in a Text.
Std: `listMap`, `listFilter`, `listFold(items, start, f)`, `listSort(items, less)` (stable). Example: `examples/fnpointers`.
