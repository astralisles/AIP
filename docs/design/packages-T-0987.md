# Packages, with MongoDB as the first (T-0987, proposal)

Status: proposal, not built. Answers "should `mongo/` be a package instead of std?" and gives open question 6 in `open-questions-T-0944.md` a first concrete shape.

## Why mongo (and db) are package-shaped
- They need a NuGet dependency (`MongoDB.Driver`, `Microsoft.Data.Sqlite`) that the compiler itself does not; std today has none.
- They are used by few programs, yet today their names, handle types (`MongoConn`, `DbConn`) and C# runtime live inside the compiler (`Checker.KnownTypes`, `CsBackend.Externs.Mongo.cs`, `CsBackend.Runtime.Mongo.cs`). Every new engine would edit the compiler.
- Supply chain (T-0936): an optional dependency should be visible and pinned where the project says it uses it, not hidden in a compiler class.

## Proposed shape
- A package is a folder `packages/<name>/` shipped beside `std/`: the same one-function-per-file `.fn` files (header-only `extern`), plus `package.txt`:
  `name`, `version`, `nuget MongoDB.Driver 3.13.0`, `handle MongoConn` (types erased to `object`), `runtime Rt.cs` (the C# part), and one `extern <fn> <template>` line per function.
- The compiler reads those lines instead of hard-coded tables; `Checker`, `CsBackend.Externs.*` and `Interp` learn nothing about mongo.
- A project uses a package by naming it in a one-line file `packages.txt` in the project folder (`mongo 3.13.0`). A package that is not named is not loaded, its names do not exist, and no NuGet reference is emitted (so the "no dependency unless used" rule stays, and the dependency list is reviewable in one file).
- Effects keep the package prefix (`mongo.*`), so the security view of a function is unchanged.

## Not decided (needs the user)
1. Declaring: `packages.txt` per project (proposed) vs. automatic when a function is called (today's behaviour).
2. Trust: only packages shipped with the compiler (proposed first step) vs. any folder a project points to (copy-in, needs a review rule).
3. Move `db/` too, as a second package (recommended, same reasons).
