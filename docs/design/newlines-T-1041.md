# Should we enforce new lines for readability? (T-1041)

Answer: **no blank-line rule; yes to the line structure the grammar already enforces; maybe later a long-line lint.**

## What is already enforced
- One definition per file, one header item per line, one `match` arm per line, one statement per line, fixed indentation
  (`bad-indent` test). A program cannot be written as one long line of statements.
- The parser ignores blank lines (`Parser.cs`), so they are allowed but never required.

## Why not require (or forbid) blank lines
- AIP files are short (one function) and read mostly by an AI through the index; blank lines add tokens, not meaning.
- A blank-line rule is a style opinion with no right answer, and every rule adds an error code, a test and a fix-up cost
  for generated code. Keep one canonical shape instead: if a style is wanted, a future `aip fmt` should produce it, not the checker reject it.

## The real readability problem
Nested calls on one line, for example `examples/shapes/main.fn` building a list with four nested `listAppend` calls (155 columns).
That is not fixed by blank lines. If we act, the best next step is a lint warning `long-line` (for example over 120 columns,
advice: split with `let`), added in `Lint.cs` with a `tests/lint/long-line` case. Not done here: it needs a decision on the limit
and would warn on existing examples.
