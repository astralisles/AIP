# Standard library layout (T-0942)

One function per file, grouped one folder deep: `std/<group>/<name>.fn`.
List them with `scripts\aip.ps1 std`.

| group       | what                                   | rule                         |
|-------------|----------------------------------------|------------------------------|
| `text/`     | Text helpers (`textSlice`, ...)        | name starts with `text`      |
| `list/`     | `List<T>`                              | name starts with `list`      |
| `map/`      | `Map<K,V>`                             | name starts with `map`       |
| `set/`      | `Set<T>`                               | name starts with `set`       |
| `sortedMap/` | `SortedMap<K,V>`, keys in order  | name starts with `sortedMap` |
| `queue/` | `Queue<T>`, first in first out | name starts with `queue` |
| `priorityQueue/` | `PriorityQueue<T>`, lowest priority first | name starts with `priorityQueue` |
| `sortedSet/` | `SortedSet<T>`, items in order   | name starts with `sortedSet` |
| `file/`     | read/write files                       | name starts with `file`      |
| `bit/`      | bit operations (`bitAnd`, `bitShl`)    | name starts with `bit`       |
| `num/`      | number conversions (`numToU8`, `numToF64`) | name starts with `num`       |
| `terminal/` | normal output (`printLine`)            | effect `terminal.*`          |
| `log/`      | diagnostics to stderr (`logLine`)      | effect `log.*`               |
| `process/`  | command line (`arg`, `argCount`)       | effect `process.*`           |
| `core/`     | language helpers (`noop`)              | only for what fits no group  |

Rules, checked by the compiler on every run (error `std-layout`):
- a std file must be in a group folder, never directly in `std/`;
- its name starts with the group name, or one of its effects starts with `<group>.`; `core/` is exempt.

Adding a function: pick the group by the type it works on (pure) or the effect it needs, put the file
there, and add its C# form to `Externs` in `CsBackend.cs`. A new group is just a new folder; add a row here.
Names stay unique across all groups (and user projects), because programs have no imports.
