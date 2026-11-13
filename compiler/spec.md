AIP spec (one page; print with `aip spec`). AIP is written by AI, read by AI, checked by the compiler.

PROJECT   a folder with main.fn. One definition per file, file name = name. No imports: every name in the folder
          or in std is visible. Find std/project functions with `aip find <projectDir> <words>`.
FUNCTION  fn name(a Int, b Text) -> Int
            "One-line doc."                 (required)
            pure | effects terminal.write, file.read   (required; lists everything the function can do)
            requires <bool expr>            (optional, repeatable)
            example name(1, "x") == 2       (optional, repeatable)
          body
            <statements, indented 2 spaces>
          std functions have `extern` instead of `body`.
TYPES     Int, Text, Bool, Unit, List<T>, Set<T>, Map<K,V>, SortedSet<T>, SortedMap<K,V> (Int or Text keys, kept in order), Queue<T> (first in, first out), PriorityQueue<T> (push with an Int priority; lowest comes out first). All values are immutable: mapSet(m, k, v) returns a new map.
STRUCT    Point.struct:  struct Point / "doc" / x Int / y Int      build: Point(1, 2)   read: p.x
GENERICS  variant Option<T> / struct Pair<A,B>: declare parameters (single capital letters) in the header; use Option<Int>.
          fn first(items List<T>) -> Option<T>: letters in a fn signature are inferred at calls. A T can only be stored, passed, returned.
VARIANT   Shape.variant: variant Shape / "doc" / Circle(r Int) / Nothing    one case per line
STATEMENTS  let x = expr        if c then a else b       match v  (one `Case(a, b) => expr` arm per line, every case)
          last expression is the return value. Text: "total {a + b}". Operators: + - * / % == != < <= > >= and or not.
FUNCTIONS `Fn<Int,Int>` is a function type; `&name` (pure, non-generic fn) is a value; call it `f(x)`. std: listMap/Filter/Fold/Sort.
EFFECTS   a function may call only functions whose effects are in its own effects line. No globals, no mutation.
ERRORS    compiler prints `error <code> <file>:<line> ...` then `fix: ...`. Fix and re-run `aip check`.
LOOP      write files -> `aip check <dir>` -> `aip run <dir>`. `aip index <dir>` lists one line per function.
