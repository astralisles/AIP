# Constants (`.const` files) (T-0941)

One named, typed, immutable value per file (`maxRetries.const`), read by name anywhere like a value. See `examples/consts`.

    const maxRetries I64
      "How many times a step is tried before giving up."
      value 3

- The value is any expression: literals, other constants, structs, pure calls (`listAppend(listEmpty(), 2)`).
- Always pure, so no effect is needed to read one (global-state-T-0925). A value that calls a function with effects is `const-not-pure`.
- A constant may use other constants, but not itself (`const-cycle`, also through functions).
- Names are unique across functions, constants and types; a parameter, `let` or match name cannot reuse a constant's name.
  Like functions, a constant must be used from `main` (`unused-const`). The index shows `const name Type | pure`.
- C# back end: a static `Const.name` property backed by `System.Lazy`, so it is computed once, on first use, and the order of files does not matter.
