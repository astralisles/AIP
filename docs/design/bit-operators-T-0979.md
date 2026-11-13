# Bit operations are operators (T-0979)

Question: should `bitAnd`, `bitOr`, `bitXor` and friends be symbols instead? Yes. They replace the `bit/` std functions.

| operator | meaning | notes |
|---|---|---|
| `a & b` | bits set in both | both sides the same whole-number type |
| `a \| b` | bits set in either | same |
| `a ^ b` | bits set in exactly one | same |
| `~a` | every bit flipped | keeps the type; for unsigned types this is max minus a |
| `a << n` | shift left, zeros come in | bits shifted out are lost |
| `a >> n` | shift right | zeros for unsigned types, sign copied for signed ones |

- Works on `Int`, `I8`..`I32`, `U8`..`U64`; the result has the type of the left side. A literal takes the type of the other side (`flags & 10`).
- The shift count `n` is always `Int`; outside `0..width-1` it is a run-time error (not C#'s silent masking).
- Precedence, tightest first: `* / %`, `+ -`, `<< >>`, `&`, `^`, `|`, comparisons, `and`, `or`, `=>`.
  This is the Python/Rust order, so `a & b == c` means `(a & b) == c` (C gets this wrong). Use parentheses when unsure.
- `<<` and `>>` are single tokens; the type parser splits `>>` so `List<List<Int>>` still works.
- Not changed: `intAnd`, `intOr`, `intXor`, `intNot`, `intShiftLeft`, `intShiftRight` in `int/` stay (Int only, shift count taken modulo 64). They now duplicate the operators; removing them is a separate decision.
- Migration: `bitAnd(a, b)` is `a & b`, `bitOr` `|`, `bitXor` `^`, `bitNot(a)` `~a`, `bitShl(a, n)` `a << n`, `bitShr(a, n)` `a >> n`.
