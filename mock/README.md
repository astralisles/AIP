# Mock files

Hand-written samples of the envisioned language. Nothing here is parsed, checked or generated: the compiler (`compiler/`) only supports `.fn` files so far, and it rejects this folder because of the `.struct`, `.variant` and `.const` files. For a program that compiles and runs, see `examples/hello`.

- `src/` — one item per file. The extension is the kind: `.struct`, `.variant`, `.const`, `.fn`. The file name is the item name, and names are unique across the project, so there are no imports.
- `index.txt` — mock of the index the compiler would generate (one line per item).

## Conventions used in the samples

- Line 1 declares the item; line 2 is a mandatory one-line purpose.
- In a `.fn` file, everything above `body` is the header: effects (`pure` or `effects ...`), `requires`, `ensures`, `example`. The index is built from headers, so callers never need the body.
- Ranges refine types: `Int 0..100`, `Int 0..` (no upper bound), `Text 1..24` (length), `List<Player> 1..8` (count).
- `match` must cover every case; there is no default branch and no null.
- `todo <Type>` is a typed hole the compiler would list as unfinished work (see `healPlayer.fn`).
- Two-space indent, single spaces, no alignment, no braces or semicolons.
- Ranges and proved contracts are erased in the compiled output; they cost nothing at run time.
