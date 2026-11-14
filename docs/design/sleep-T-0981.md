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

## Review: C# warns against sleeping in a task (thread-pool starvation)

That advice is for `async` code on the shared thread pool, where a sleeping task holds a pool thread other work
could use. AIP differs: no `async`/`await` (see `parallelism-T-0935.md`), and the generated program runs on
dedicated threads (`new Thread`, `CsBackend.cs`), not the pool. A sleeping thread there starves nobody.
`taskSpawn`/`taskAwait` are not in the runtime yet, so nothing can be starved today.

Decision:
1. `timeSleep` is implemented as `Thread.Sleep` and is the only blocking wait; the user never writes `Task.Delay`/`await`.
2. Rule for the future task runtime: each AIP task gets its own thread (or a pool sized to the task count). Do NOT
   run AIP tasks on the shared .NET thread pool, or sleeps (and `tcpReceive`, `tcpAccept`, which block the same way)
   would starve it.
3. If tasks later move to a cooperative scheduler, `timeSleep` becomes a scheduler call that parks the task
   (internally `Task.Delay`); the AIP signature and effect do not change, so no program breaks.
4. Prefer a real wait over polling where one exists (`udpReceive` timeout, `taskAwait`); `timeSleep` is for
   backoff and rate limiting only.
