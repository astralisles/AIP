# User-defined generics (T-0949)

**Status: implemented (T-0949): generic struct/variant and generic fn. See `examples/generics`.**

## Before this ticket
`List<T>`, `Set<T>`, `Map<K,V>` were built in; type variables (`T`, `K`, `V`) were accepted only in `std` headers
(`Checker.IsKnown`). Generics are erased in the C# back end. See `compiler/README.md`.

## Question
Should user code be able to declare type parameters?

## Decision (recommended): yes, in a small form
Reason: `Option<T>` and `Result<T,E>` (open-questions item 5) cannot be written without them, and without them every
program that needs "a pair of X and Y" or "a maybe-X" copies a type per element type. The tie-breaker is item 12 (the
user is the AI): generics must be uniform and unambiguous, so only the small form is allowed.

Rules:
1. **Declared, not guessed.** Type parameters are listed in the header, as single capital letters, e.g.
   `variant Option<T>` with cases `Some(value T)` and `None`; `fn first(items List<T>) -> Option<T>`.
2. **Types first.** Step 1: generic `struct`/`variant`. Step 2: generic `fn`. Step 2 is cheap because `Unify`/`Subst`
   already infer type variables for std functions; user functions reuse them.
3. **No constraints, no traits.** A `T` can be stored, passed and returned, nothing else. Operations that need
   equality or ordering stay in the std functions that have them. This avoids the biggest source of complexity.
4. **Inferred at calls, never written there.** `first(xs)` infers `T`; there is no `first<I64>(xs)` spelling, so there is
   one way to write a call. Where nothing fixes the type (e.g. `None()` alone) it stays unknown (`_`, as for
   `listEmpty()`) and matches any type; it becomes concrete when combined with a typed value.
5. **No effect on safety.** Type parameters add no reach outside the program, so the security rule is unaffected.
   The back end keeps erasing, so no code-size growth and no new runtime paths.
6. **Index shows them.** The generated index prints the header with its parameters, so a model sees `Option<T>` there.

## Costs / risks
- The checker must treat a parameter as an opaque type inside the body (new error code `bad-type-param`).
- Erasure means a value's concrete type is lost at run time; fine because the checker is the only enforcement and
  there is no reflection.
- Does not give polymorphic function pointers (see `function-pointers-T-0928.md`); revisit separately.

## Not decided
Whether `_` (unknown element type) should be allowed in user signatures: recommend no, it is internal to inference.

## As built
- Type definitions declare parameters: `variant Option<T>`, `struct Pair<A,B>`. Using a generic type without arguments
  is `unknown-type`; wrong parameter names or duplicates are `bad-type-param`.
- Functions use single capital letters in their signature with no declaration. A letter in the result that no
  parameter has is `bad-type-param`.
- Inside a generic body a `T` value cannot be compared (`==`) or put into a text (`type-mismatch`).
- Back end: a generic type is one erased C# record (fields of type `T` are `object`); reads cast back to the
  real type (`Erased` flag on `NameRef`, `Field`, `Call`). Tests: `examples/generics`,
  `tests/errors/{bad-type-param,generic-missing-args,generic-compare}`.
