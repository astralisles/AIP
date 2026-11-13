# Std functions missing compared with C / C++ (T-0957)

Today's std (45 functions): `text` (5), `list` (4), `map` (7), `set` (6), `sortedMap` (7), `sortedSet` (6), `queue` (5),
`priorityQueue` (5), `file` (2), `terminal` (1), `log` (1), `process` (2), `core` (1). Only `I64`, `Text`, `Bool`, `Unit` exist.

Not a goal: pointers, `malloc`/`free`, `memcpy`, `setjmp`, `volatile`, threads/atomics (see `parallelism-T-0935.md`),
`printf` format strings, iterators, `std::string_view`, anything that mutates in place. Everything is immutable and effects show in signatures.

Ordered by value for bootstrapping the compiler (`bootstrapping.md`) and for general programs.

## 1. Needed soon (pure, no new type)
- **Text** (`<string.h>`, `std::string`): `textFind`, `textFindLast`, `textContains`, `textStartsWith`,
  `textEndsWith`, `textSplit` (-> `List<Text>`), `textJoin`, `textTrim`, `textReplace`, `textRepeat`, `textToUpper`, `textToLower`,
  `textCompare`, `textReverse`, `textIsDigit`/`textIsAlpha`/`textIsSpace` (`<ctype.h>`, on a code), `textFromInt` (`itoa`, `std::to_string`).
- **I64 math** (`<stdlib.h>`, `<algorithm>`): `intMin`, `intMax`, `intAbs`, `intClamp`, `intPow`, `intSign`, `intGcd`; bit ops
  `intAnd`, `intOr`, `intXor`, `intNot`, `intShiftLeft`, `intShiftRight` (needed for hashing).
- **List** (`<algorithm>`, `<vector>`): `listSet`, `listInsert`, `listRemoveAt`, `listConcat`, `listSlice`, `listReverse`, `listSort`
  (needs a comparison function pointer, `function-pointers-T-0928.md`), `listMap`/`listFilter`/`listFold` (same), `listIndexOf`,
  `listContains`, `listFirst`/`listLast`, `listRange` (`std::iota`). Today a list can only be built by append and read by index.
- **Map/Set**: `mapValues`, `mapMerge`, `setUnion`, `setIntersect`, `setDifference`, `setIsSubset`, `sortedMapFirstKey`/`LastKey`,
  `sortedMap`/`sortedSet` range queries (`lower_bound`/`upper_bound`).
- **Queue**: double-ended (`std::deque`): `dequePushFront`, `dequePopBack`; a `Stack<T>` (`std::stack`) is just a list, document that instead.

## 2. Needs a decision first (new type or effect)
- **Float / math** (`<math.h>`, `<cmath>`): `sqrt`, `pow`, `floor`, `ceil`, `round`, `sin`, `cos`, `tan`, `atan2`, `exp`, `log`, `fabs`,
  `fmod`, `isnan`; needs `Float` (open question 9). Also `strtod`/`atof` (`textToFloat`) and float formatting.
- **Bytes / Unicode**: `Bytes` type, `fileReadBytes`, `fileWriteBytes`, UTF-8 encode/decode (`textToBytes`, `textFromBytes`).
- **Failure**: `intParse`/`textToInt` return `Option`/`Result` instead of crashing once generic variants exist (open question 5).
  Same for `mapGet`, `listGet`, `fileRead`.
- **Time** (`<time.h>`, `<chrono>`): `timeNow` (effect `time.read`), `timeSleep`, monotonic clock for profiling. Breaks replay, so effect-gated.
- **Random** (`<random>`, `rand`): `randomInt` (effect `random.read`), seeded pure generator `randomNext(seed)` which needs no effect.
- **Environment** (`getenv`): `envGet` (effect `process.env`).
- **Files and directories** (`<stdio.h>`, `<filesystem>`): `fileAppend`, `fileExists`, `fileDelete`, `fileRename`, `fileSize`, `dirList`,
  `dirCreate`, `pathJoin` (pure), `pathParent`, `pathExtension`, `fileReadLines`. Effects `file.read`/`file.write` with a path scope (`security-T-0936.md`).
- **Terminal input**: `readLine` (`getline`, `std::cin`, effect `terminal.read`), `print` without newline, `printError`.
- **Process**: `processExit(code)` (`exit`), `processRun` (`system`/`popen`, effect `process.run`; also needed by bootstrapping step 4), `processEnvAll`.
- **Network** (sockets, `<arpa/inet.h>`): effect `net.*`; not before the security model has scopes.

## 3. Algorithms and utilities worth having (all pure, build on section 1)
`sort`, `binarySearch`, `min/max` of a list, `sum`, `count`, `all/any`, `unique`, `zip`, `partition`, `rotate`; `Pair`/`Tuple` (`std::pair`)
as a std `struct`; `Option` (`std::optional`) and `Result` (`std::expected`) as std variants; hash (`std::hash`) -> `textHash`; `bitset`
as `Set<I64>`; `regex` (low priority: text helpers cover the compiler); `assert` is already `requires`/`ensures`.

## Recommendation
1. Do section 1 text, I64 and List first: about 35 small files plus `Externs` entries, no language change; unblocks the AIP-written parser.
2. Then decide `Float` and `Option`/`Result` (open questions 5 and 9) since many section 2 functions depend on them.
3. Add `processExit`, `fileExists`/`fileAppend`/`dirList`, `readLine` and the clock/random effects together as one "outside world" ticket.
4. Add `listSort`/`map`/`filter`/`fold` when function pointers land.
