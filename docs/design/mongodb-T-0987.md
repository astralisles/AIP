# MongoDB (T-0987)

- Separate from the relational `db/` family (see `databases-T-0987.md`): documents are not rows, so MongoDB has its own std family `mongo/`.
- Functions: `mongoOpen(url, database)`, `mongoInsert(conn, collection, doc)` (returns the `_id` text), `mongoFind(conn, collection, filter)` (`List<Text>`, one JSON per document),
  `mongoUpdate(conn, collection, filter, update)` and `mongoDelete(conn, collection, filter)` (counts), `mongoDrop(conn, collection)`, `mongoClose`.
  Effects `mongo.connect`, `mongo.read`, `mongo.write`, `mongo.close`; built-in handle type `MongoConn` (erased to `object`).
- Documents, filters and updates are JSON text (MongoDB extended JSON, relaxed on output). No BSON type in the language yet; a value is read back by parsing the JSON text.
- Back end: the generated program references the `MongoDB.Driver` NuGet package (3.13.0) **only when the project calls a mongo function** (`UsesMongo`); db and mongo packages combine in `CsprojWithPackages`.
- `mongoOpen` pings the server, so a wrong url fails at once. Connection options (TLS, user and password) go in the url as usual (`mongodb://user:pw@host/?tls=true`).
- Injection: filters are JSON text, so never join untrusted text into one; build the document from fixed text and put values through a JSON-escaping step (a std `jsonQuote` is a future addition).
- `aip test` refuses the functions. Test: `tests/checks/mongo.ps1` runs `examples/mongo` when `MONGO_URL` is set, else skips.
- Not done: aggregation pipelines, indexes, find options (sort, limit, projection), transactions, a typed document value, the async API.
