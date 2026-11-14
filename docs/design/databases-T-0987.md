# Databases (T-0987)

- Decision: the first support is **relational and engine-neutral**. NoSQL stores (MongoDB, ...) are too different
  (documents, not rows and SQL) and are a separate, later design; they are not covered here.
- Std family `db/`: `DbConn` handle (built in, erased to `object`), `dbOpen(url)`, `dbExec(conn, sql, args)` (rows changed),
  `dbQuery(conn, sql, args)` (`List<List<Text>>`, NULL = empty text), `dbClose`. Effects `db.connect`, `db.write`, `db.read`, `db.close`.
- Values travel as `Text` and parameters are `?` marks filled from `args` (never build SQL by joining text: injection).
  The `url` picks the engine: only `sqlite:<file>` and `sqlite::memory:` so far. Another engine is a new URL scheme in `Rt.DbOpen`.
- Back end: the generated program references the `Microsoft.Data.Sqlite` NuGet package **only when the project calls a db function**
  (`UsesDb` in `CsBackend.Externs.Db.cs`); other programs build exactly as before. The first build needs the package in the NuGet cache or network.
- `aip test` refuses the functions (effectful handles). Example `examples/db`.
- Not done: typed columns (everything is Text), transactions, prepared-statement reuse, a `?` inside an SQL string literal is treated as a parameter mark, other engines (PostgreSQL), NoSQL.
