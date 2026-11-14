# Anatomy of an AIP project's files (T-0948)

An AIP project is a folder. There are no imports: every definition in the folder, plus the standard
library, is visible everywhere. **One definition per file; the file name is the definition's name.**

## Files you write

| File | Defines | Example |
|---|---|---|
| `main.fn` | The entry point: `fn main() -> Unit`, no parameters. Its `effects` line lists everything the program can do. Every function must be reachable from `main`. | `examples/hello/main.fn` |
| `<name>.fn` | One function. | `examples/shapes/area.fn` |
| `<Name>.struct` | A record type with named fields. | `examples/shapes/Point.struct` |
| `<Name>.variant` | A tagged union (one case per line). | `examples/shapes/Shape.variant` |
| `expected-output.txt` | Optional. Exact output of `aip run`, one line per printed line; `scripts\test.ps1` checks it. | `examples/shapes/expected-output.txt` |

Any other file type (for example `.const`) is rejected with `unsupported-file`.

## Parts of a `.fn` file

    fn grade(score I64) -> Text          1. signature: name, typed parameters, return type
      "A letter for a score."            2. doc: exactly one line, required (error `missing-doc`)
      pure                               3. effects: `pure` or `effects terminal.write, file.read`; required.
                                            A function may only call functions whose effects are in its own list.
      requires score >= 0                4. optional, repeatable: precondition (a Bool expression)
      example grade(95) == "A"           5. optional, repeatable: a checked example; `aip test` runs them
    body                                 6. the word `body` at column 0 starts the code
      if score >= 90 then "A" else "F"      (std functions write `extern` here instead: header only)

- Header lines (2-5) are indented 2 spaces; body statements are indented 2 spaces under `body`.
- Types may carry a range, for example `Text 1..` (non-empty) or `I64 0..100`. Ranges, `requires` and `example` are type-checked
  and run by `aip test`; they are not proved.
- The body is statements: `let x = expr`, calls, `if c then a else b`, `match`. The last expression is the return value.
  Text can embed expressions: `"total {a + b}"`. There are no loops (use recursion), no globals, no mutation.

## Parts of a `.struct` file

    struct Point                 name (must equal the file name)
      "A position on a grid."    one-line doc, required
      x I64                      one field per line: name, type
      y I64

Build with `Point(1, 2)`, read with `p.x`.

## Parts of a `.variant` file

    variant Shape
      "A shape: a circle, a rectangle, a dot, or nothing."
      Circle(center Point, radius I64)     one case per line, with typed fields
      Nothing                              or no fields (construct and match as `Nothing()`)

Consume with `match`: one `Case(a, b) => expr` arm per line, and every case is required.

## Files the tools produce (not committed)

`<project>/build/` is generated and git-ignored: `index.txt` (one line per function: signature, effects, "checked, not proved"),
`cs/` (the emitted C#), `bin/` (the built program) and cache hashes so unchanged projects are not rebuilt.
`aip index <dir>` prints the index; `aip emit <dir>` writes the C# without building.

## Standard library files

`compiler/std/<group>/<name>.fn`: same `.fn` format but with `extern` instead of `body`; the implementation lives in
`Externs` in `CsBackend.cs`. See `compiler/std/README.md` for the groups and naming rules.

## Test files in the repo

| Path | Meaning |
|---|---|
| `examples/<name>/expected-output.txt` | exact program output |
| `tests/errors/<case>/expected.txt` | text `aip check` must print when it rejects the project |
| `tests/lint/<case>/expected.txt` | warning `aip lint` must print |
| `tests/contracts/<case>/expected.txt` | failure `aip test` must report |
| `tests/checks/<feature>.ps1` | any other check |
