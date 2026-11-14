# Standard constants (T-0993)

Std may hold `.const` files like any project: `compiler/std/num/numPi.const`, `numE.const`, `numTau.const` (all F64).
Use them by name, with no import: `numPi * r * r`. They follow the std layout rule (name starts with the group, `num`).
They are never reported as `unused-const` and do not appear in the project index. The C# back end emits them
as `Const.numPi` etc. (lazy). The compiler build copies `std/**/*.const` next to `std/**/*.fn`.
