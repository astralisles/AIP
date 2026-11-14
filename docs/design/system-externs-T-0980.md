# Process, file, time, random and environment functions (T-0980)

All are `extern` std functions with an effect, so they show in the signature and `aip test` refuses them.

| Function | Effect | Notes |
|---|---|---|
| `processExit(code Int) -> Unit` | `process.exit` | Writes profile/call logs, then exits with the code. |
| `fileExists(path Text) -> Bool` | `file.read` | True for files and folders. |
| `fileAppend(path Text, content Text) -> Unit` | `file.write` | `fails`; creates the file. |
| `dirList(path Text) -> List<Text>` | `file.read` | `fails`; names only, sorted by ordinal text order. |
| `timeNow() -> Int` | `time.read` | Milliseconds since 1970 UTC. |
| `randomInt(low Int, high Int) -> Int` | `random.read` | `fails`; `low <= n < high`. Not seedable yet. |
| `envGet(name Text) -> Text` | `process.env` | Empty text if unset. |
| `envHas(name Text) -> Bool` | `process.env` | Tells "unset" from "empty". |

Code: `CsBackend.Externs.System.cs`, `CsBackend.Runtime.System.cs` (`Rt.System.cs`). Not done: path scopes for file effects
(`security-T-0936.md`), `timeSleep`, a seeded pure generator, `processRun`.
