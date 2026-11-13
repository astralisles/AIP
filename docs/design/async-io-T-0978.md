# Design decision: async IO for network, files, etc. (T-0978)

Do not add `async`/`await`. IO stays blocking in the language; concurrency comes from tasks
(T-0935). Nothing is implemented by this note.

Why: `async` colors every caller of an async function, which fights "one function per file, effects
in the signature". The effect line already says a function does IO (`net.*`, `file.*`, `terminal.read`);
a second marker saying "and it may suspend" adds no information an AI reader needs, and doubles std
(`fileRead` and `fileReadAsync`).

Rules:
1. Every IO function is a plain blocking call with its effect, as today (T-0959). Source reads top to bottom.
2. Waiting on several things at once uses `taskRun` / `taskAwait` (T-0935): one task per connection or file.
   The `parallel` effect in the signature shows which functions fork. Tasks share nothing; results are
   consumed in a program-chosen order.
3. A blocked call must be bounded. Every blocking receive gets a timeout variant returning empty on
   timeout, like `udpReceive(socket, timeoutMs)`: add `tcpReceiveTimeout(conn, max, timeoutMs)` and
   `tcpAcceptTimeout(listener, timeoutMs)` (T-0959 lists TCP timeouts as not done). Timeouts cover
   cancellation: a task loops on a short timeout and checks a flag passed in as an argument.
4. No callbacks, no event loop, no channels, no futures exposed to programs. A `Task<R>` is the only
   handle, and it is awaited exactly once (unawaited is a compile error, T-0935).
5. Back end decides the mechanism. C# uses threads (blocking sockets and files); the thread count and
   any use of OS-level async underneath are invisible to the program. A sequential back end must give
   the same result, so programs may not depend on tasks running at the same time (for example, a
   server task and its client in one process must be the only case that needs real concurrency, and it
   is documented where used).
6. Files: read/write whole-file functions stay blocking. Large or streaming files get a handle type
   (like `TcpConn`) with `read(handle, max)`, introduced when a program needs it, not before.

Order of work: (a) `Fn` (T-0928), (b) `taskRun`/`taskAwait`, (c) TCP timeout variants, (d) file handles.
Step (c) is independent and small, and can ship first.

Rejected: `async`/`await` (coloring, duplicate std), callbacks/event loop (inverts control flow, hard to
read for the index), channels (shared state, T-0925), non-blocking poll/`select` (error-prone; tasks plus
timeouts cover it).
