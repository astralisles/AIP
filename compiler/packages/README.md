# packages

Optional libraries shipped beside `std/`. A project uses one by listing it, with the exact version, in a
`packages.txt` in the project folder (one `<name> <version>` per line, `#` comments):

    mongo 1.0.0

A package that is not listed adds no functions, no types and no NuGet reference. An unknown name or a version
that is not the shipped one is a compile error (`unknown-package`, `package-version`).

A package is a folder `packages/<name>/` with:
- `<fn>.fn`: header-only `extern` functions, one per file (as in std; no group-name rule);
- `package.txt`: `name`, `version`, `nuget <id> <version>` (pinned), `handle <Type>` (opaque type, erased to `object`), `extern <fn> <C# template>`;
- `Rt.cs`: the C# runtime, emitted as `Rt.<name>.cs` into the generated program.

Packages today: `db` (relational, SQLite), `mongo` (MongoDB). Design: `docs/design/packages-T-0987.md`.
`aip test` refuses their effectful functions like any other effect.
