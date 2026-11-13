# Design decision: global state (T-0925)

No global mutable state. There are no global variables; all values are immutable and
a function sees only its parameters and constants. Reasons: each function must be
understandable from its file and signature alone (the language's core promise), results
must be reproducible for `example`/`ensures` checking, and the bootstrapped compiler
must be deterministic (stage 2 output == stage 1 output).

Allowed: immutable `.const` files (compile-time constants, no effect needed).

Needed state goes through an explicit, effect-tracked handle, not a hidden global: a
program that wants a counter, cache or config declares an effect (e.g. `state.read` /
`state.write`) and passes the handle/value as a parameter, so it shows in the index.
Not implemented yet; revisit only if the compiler-in-AIP proves too clumsy without it.
