# Do we need a sleep? (T-0981)

**Answer: yes, a minimal one, but not yet urgent.** Add `timeSleep(ms Int) -> Unit` with effect `time.sleep`.

Why:
- Programs that poll, retry (network, files) or rate-limit cannot do it otherwise. A busy loop burns CPU and
  `udpReceive(socket, timeoutMs)` is a hack for waiting.
- Tests such as `tests/checks/net.ps1` already need `Start-Sleep` in PowerShell to wait for a server; with `timeSleep`
  an AIP client could do the retrying itself.
- It is cheap: one extern per backend, no language change, and it fits the existing rule that anything reaching outside
  the program is an effect in the signature.

Rules:
- Own effect `time.sleep`, separate from `time.read` (`timeNow`, see `std-gaps-vs-c-T-0957.md`), so a caller can allow
  waiting without allowing clock reads (a clock read breaks replay; a sleep alone does not change results).
- Negative or zero `ms` returns at once. Blocks only the calling task, never other `taskSpawn` tasks.
- Pure computation must not need it: no sleeping to "wait for a task"; use `taskAwait`.

Implementation sketch (not done in this ticket): `compiler/std/time/timeSleep.fn` (header only, `extern`), a
`CsBackend.Externs.Time.cs` line (`Thread.Sleep`) plus a `case` in `Interp.Externs.Time.cs`, an example and a
`tests/checks/` test measuring that at least `ms` elapsed.
