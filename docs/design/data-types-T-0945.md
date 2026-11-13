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
- **No implicit mixing or widening**: `I32 + I8` is a `type-mismatch`. Convert with `numTo<T>(Int)` and
  `numFrom<T>(T) -> Int` (std group `num/`); a value that does not fit is a run-time error.
- **Literals** take the type wanted by the argument, field, return type or other operand if the value fits;
  otherwise error `out-of-range`. A literal above the `Int` maximum has type `U64`.
- **Overflow is an error, never a silent wrap**: `+ - * / %` on sized types are `checked` in the C# back end.
  Plain `Int` arithmetic still wraps (unchanged; revisit separately).
- Unary `-` needs a signed type.
- Not yet: floats (`F32`/`F64`), `I64` alias, bit operations, literals inside `List<U8>` element positions
  (use `numToU8`), range checking of `Int 0..255`-style ranges on the sized types.
