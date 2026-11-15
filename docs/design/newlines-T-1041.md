# Should we enforce new lines for readability? (T-1041)

Answer: **yes for `if` statements. A one-line `if ... then ... else ...` whose branches are both Unit is rejected
(`one-line-unit-if`); write the block form.** There is still no blank-line rule.

    if a > 0 then
      doX()
    else
      doY()

not `if a > 0 then doX() else doY()`.

## What is enforced
- Already: one definition per file, one header item, `match` arm and statement per line, fixed indentation.
- New (T-1041): a one-line `if` that gives a value (`let x = if c then 1 else 2`) is still fine, because a block cannot
  be a value. One that does something (both branches Unit) must be a block.
- Not enforced: blank lines (the parser ignores them; they add tokens, not meaning, for an AI reader).

## Open
- `else if` chains of Unit branches on one line are still accepted, because the block form cannot nest an `if` inside a branch yet
  (and `compiler/aipc` has its own body parser that would need the same change). Seen in `examples/net/main.fn` and
  `compiler/aipc/{main,body/printBody,body/printHeader}.fn`. Next step: allow `else if c then` as a block line, then reject chains too.
- Long nested-call lines (for example the list build in `examples/shapes/main.fn`) are a separate problem: a `long-line` lint.
