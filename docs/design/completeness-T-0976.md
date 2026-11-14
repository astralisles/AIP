# What else do we need to be complete? (T-0976)

"Complete" here means the definition in `open-questions-T-0944.md` item 13: a model builds a 500-line tool from `aip spec`
alone, and the AIP compiler can compile itself. Checked against the repo on 2026-11-13. Ordered by how much each blocks.

## Already there
I64/sized ints/floats/bits, Text, Bytes, List/Set/Map/SortedMap/SortedSet/Queue/PriorityQueue, structs, variants, generics,
`match`, consts, contracts (`requires`/`ensures`/`example` run by `aip test`), lint, fuzz, logging, profiling, tcp/udp,
native libraries, hash, `aip query/find/spec`, partial self-hosting (lexer, header and body parser in `examples/aipc`).

## Missing, in order
1. **Function pointers + `listMap`/`listFilter`/`listFold`/`listSort`.** **Done (T-0976 #2).** See `function-pointers-T-0928.md`.
2. **`Option`/`Result` in std, and failing std calls using them** (`listGet`, `mapGet`, `textToInt`, `fileRead`). Decided
   (`open-questions-T-0944.md` item 5); **Partly done (T-0976 #2):** std `Option<T>`, `Result<T,E>`, `listTryGet`, `mapTryGet`. Still crashing: `textToInt`, `fileRead` (need extern try-versions).
3. **Tail-call lowering** (`loops-and-switches-T-0975.md` step 1). Loops are recursion, so long loops overflow the stack.
4. **"Outside world" std**: `processExit`, `fileExists`/`fileAppend`/`dirList`/`dirCreate`, `timeNow`/`timeSleep`, `randomInt`
   (seeded pure `randomNext`), `envGet`, `printError`. None exist (only `fileRead`, `fileWrite`, `printLine`, `readLine`, `arg`).
5. **Float math** (`sqrt`, `pow`, `floor`, `sin`...) and `textToFloat`: Float types exist, math functions do not.
6. **Compiler stages in AIP**: project loader, checker, IR lowering, native codegen + linker, driver, stage-2 equals stage-1
   check (`bootstrap-gaps-T-0954.md` section C). Struct/variant `Map` keys and structural `==` (A5) are needed first.
7. **Packages**: how to depend on other people's code (open question 6) and a language version line (7), with a supply-chain answer.
8. **Evidence that it works for models**: the 20-task benchmark (question 1), JSON error mode (2), and the 500-line tool test (13).
9. **Smaller**: Unicode text, `match` on literals, set algebra (`setUnion`...), deque, `Pair`/`Tuple`, `textFromInt` already exists.

## Recommendation
Do 1, 2 and 3 next (they unblock the AIP-written compiler and remove crashes as the failure mode), then 4 as one ticket, then 8
to learn whether the language is good for models before investing in 6.

No code changed; this is a note. Recommend merging it.
