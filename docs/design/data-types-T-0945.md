# Exact-size whole numbers (T-0945)

C's `int` has no fixed size. AIP names the size in the type.

| type | bytes | range |
|------|-------|-------|
| `I8`  | 1 | -128 to 127 |
| `I16` | 2 | -32768 to 32767 |
| `I32` | 4 | -2147483648 to 2147483647 |
| `Int` | 8 | signed, -9223372036854775808 to 9223372036854775807 (what C would call `int64_t`) |
| `U8`  | 1 | 0 to 255 |
| `U16` | 2 | 0 to 65535 |
| `U32` | 4 | 0 to 4294967295 |
| `U64` | 8 | 0 to 18446744073709551615 |

Rules:
- **Signed is explicit**: `I` is signed, `U` is unsigned. `Int` stays the everyday 64-bit signed type, so no
  existing program changes. There is no `I64` alias (one name per type).
- **Widening is automatic, narrowing is explicit** (iteration 4, see the end of this file). `I32 + U32` is a `type-mismatch`;
  convert with `numTo<T>(Int)` and `numFrom<T>(T) -> Int` (std group `num/`); a value that does not fit is a run-time error.
- **Literals** take the type wanted by the argument, field, return type or other operand if the value fits;
  otherwise error `out-of-range`. A literal above the `Int` maximum has type `U64`.
- **Overflow is an error, never a silent wrap**: `+ - * / %` on sized types are `checked` in the C# back end.
  Plain `Int` arithmetic still wraps (unchanged; revisit separately).
- Unary `-` needs a signed type.
- Not yet: `I64` alias, literals inside `List<U8>` element positions
  (use `numToU8`), range checking of `Int 0..255`-style ranges on the sized types.

## Floats and bit operations (iteration 2)

| type | bytes | notes |
|------|-------|-------|
| `F32` | 4 | IEEE 754 single |
| `F64` | 8 | IEEE 754 double |

- A literal with a point (`2.5`, digits on both sides) is `F64`, or `F32` when an `F32` is wanted. `2` is never a float: write `2.0`.
- `+ - * / %`, comparisons and unary `-` work on one float type at a time; no mixing with each other or with whole numbers.
  Convert with `numToF32/numToF64(Int)`, `numFromF32/numFromF64(float) -> Int` (drops the fraction; NaN, infinity or out of range is a run-time error), `numF32ToF64`, `numF64ToF32`.
- Floats follow IEEE 754: overflow gives infinity and `x / 0.0` gives infinity or NaN, not an error. Only the conversion to `Int` is checked.
- Bit operations are std functions in `bit/`, generic over any whole-number type (`Int`, `I8`..`U64`) and keep the type:
  `bitAnd`, `bitOr`, `bitXor`, `bitNot`, `bitShl(a, count)`, `bitShr(a, count)`. No new operators.
  `bitShr` is logical for unsigned and arithmetic (sign copied) for signed types. Bits shifted out of `bitShl` are dropped
  (that is what shifting means), but a count outside `0..width-1` is a run-time error instead of C#'s silent masking.
  A literal argument takes the type of the other argument: `bitAnd(flags, 10)`.

## Lossless widening (iteration 4)

Iteration 3 left no implicit conversions at all. Iteration 4 allows the ones that cannot lose a value ("upcasting"):

| from | widens to |
|------|-----------|
| `I8` | `I16`, `I32`, `Int` |
| `I16` | `I32`, `Int` |
| `I32` | `Int` |
| `U8` | `U16`, `U32`, `U64`, `I16`, `I32`, `Int` |
| `U16` | `U32`, `U64`, `I32`, `Int` |
| `U32` | `U64`, `Int` |
| `F32` | `F64` |

- Applies to arguments, return values, struct/variant fields, and the operands of `+ - * / % < <= > >= == !=`
  (the result of arithmetic is the wider type: `I8 + I32` is `I32`).
- Never implicit: narrowing (`Int` to `I32`), signed with unsigned when neither fits the other (`U32 + I32`, `I8 + U8`),
  `U64` to anything else, and any whole number to a float (`F64 + Int`). These stay `type-mismatch`; convert with `numTo*`/`numFrom*`.
- Not applied inside `List<T>`, `Set<T>`, `Map<K,V>` or generic parameters (`List<I8>` is not a `List<Int>`).
- Literals still take the wanted type if they fit (`out-of-range` otherwise). Tests: `int-width-mismatch`, `int-width-argument`,
  `float-width-mismatch`, example `widening`.
