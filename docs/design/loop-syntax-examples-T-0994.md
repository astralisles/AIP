# Loop syntax candidates, side by side (T-0994)

None of the syntax below exists in the compiler yet; only style 1 runs today. Same four tasks in each style.
Tasks: (a) sum 1..n, (b) count evens with a condition, (c) nested: sum of the r*c table, (d) nested with an effect: print the table.

## 1. Recursion (today, `examples/control`, `examples/nested-loops`)
    fn sumTo(n Int, total Int) -> Int
      if n == 0 then total else sumTo(n - 1, total + n)

    fn rowSum(r Int, c Int, cols Int, total Int) -> Int
      if c > cols then total else rowSum(r, c + 1, cols, total + r * c)
    fn tableSum(r Int, rows Int, cols Int, total Int) -> Int
      if r > rows then total else tableSum(r + 1, rows, cols, rowSum(r, 1, cols, total))
Pro: nothing new. Con: nested loop = two functions, state passed by hand, loop shape hidden in an `if`.

## 2. `fold` (value loop) and `for` (effect loop), proposals A and B
    # (a)
    fold i in 1..n with total = 0
      total + i
    # (b)
    fold i in 1..last with count = 0
      if i % 2 == 0 then count + 1 else count
    # (c)
    fold r in 1..rows with total = 0
      fold c in 1..cols with total = total
        total + r * c
    # (d)
    for r in 1..rows
      for c in 1..cols
        printLine("{r} x {c} = {r * c}")
Pro: shortest, state named in the header, nesting is indentation. Con: two keywords; `fold` needs the reader to know that the body is the next state.

## 3. One `loop` with explicit `next` / `done` (while-like)
    # (a)
    loop i = 1, total = 0
      if i > n then done total else next i + 1, total + i
    # (c)
    loop r = 1, total = 0
      if r > rows then done total
      else next r + 1, loop c = 1, t = total
        if c > cols then done t else next c + 1, t + r * c
Pro: one keyword, covers early exit and unusual steps (`next i * 2`). Con: nested form is the hardest to read; `done` and `next` are easy to get wrong.

## 4. Higher-order std functions (needs `Fn` pointers, `function-pointers-T-0928.md`)
    # (a)
    listFold(range(1, n), 0, fn(total, i) => total + i)
    # (c)
    listFold(range(1, rows), 0, fn(total, r) => listFold(range(1, cols), total, fn(t, c) => t + r * c))
Pro: no new grammar. Con: needs lambdas, nested calls get dense, no clean effect loop.

## Recommendation
Style 2: it reads closest to what people expect from `for`, keeps state visible, and desugars to style 1 (so tail-call lowering applies).
Style 3 only if early exit turns out to matter; style 4 can be added later without grammar.
