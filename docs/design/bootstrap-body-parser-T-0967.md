# Bootstrapping step C1: body parser in AIP (T-0967)

Done: `examples/aipc/body/` reads `requires` / `ensures` / `example` lines and function bodies into an expression tree
(`Expr` variant: numbers, texts with `{expr}` holes, names, calls, `.field`, unary and binary operators with the C#
precedence, `if/then/else`, the subject of a `match`). `aipc ast <file>` prints a dump; `aip ast <file>` (C#, `AstDump.cs`)
prints the same dump from the C# parser. `scripts\compare-ast.ps1` (run by `tests/checks/bootstrap-body-parser.ps1`)
requires them equal for every `.fn` file the C# parser accepts (371 files; 16 error-test files are skipped).

Dump format: one line per `requires `/`ensures `/`example ` line (in that order), then per body line `stmt [let NAME ]EXPR`,
`arm Case[a,b] EXPR` (match arms in source order, nested arms after their parent), `if COND` / `else` for a multi-line `if`.
Expressions are s-expressions: `(int 5)`, `(float 1.5)`, `(bool true)`, `(text "lit" EXPR ...)`, `(name x)`, `(call f a ...)`,
`(field e name)`, `(unary op e)`, `(binary op l r)`, `(if c a b)`, `(match subject)`.

Limits (not done yet):
1. The dump is flat: the AIP side does not yet build the nested statement / arm / block tree (it tracks open matches and
   `if` blocks only to read lines correctly), so nesting depth is not compared.
2. Only well-formed input is handled; parse errors (the `syntax` etc. codes) are not reported by the AIP parser.
3. Still to write for C1: `.struct`, `.variant`, `.const` files, the `native` header line, and the function signature
   into the same tree. Then C2 (checker) in `bootstrap-gaps-T-0954.md`.
4. Floats and ints are compared as token text on the AIP side; `aip ast` prints the C# value (`1.50` would differ).
