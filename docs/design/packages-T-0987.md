# Packages, with `mongo` and `db` as the first (T-0987)

Status: built. Gives open question 6 in `open-questions-T-0944.md` a first answer for the compiler-shipped case.

## Why mongo and db are packages, not std
- They need a NuGet dependency (`MongoDB.Driver`, `Microsoft.Data.Sqlite`); std has none.
- Few programs use them, yet their names, handle types and C# runtime used to live inside the compiler. Now a new engine is a new folder.
- Supply chain (T-0936): the dependency list of a project is one reviewable file, pinned.

## Shape
- `compiler/packages/<name>/`: `.fn` headers, `package.txt` (name, version, pinned `nuget`, `handle`, `extern` templates), `Rt.cs`. See `compiler/packages/README.md`.
- The project's `packages.txt` (`mongo 1.0.0`) is the manifest of used packages and their versions. The version must equal the shipped one exactly, so a package change is visible in the project's diff.
- Not listed means not loaded: its functions do not exist (`unknown-name`) and no NuGet reference is emitted.
- Effects keep the package prefix (`mongo.*`, `db.*`), so security review reads the same as for std.
- Front end stays target independent: the checker only learns handle types from the manifest; templates and `Rt.cs` are used by the C# back end.

## Decisions taken (user asked what to suggest)
1. Declaration: per-project `packages.txt` with name and exact version (yes, a manifest is needed: it is the only place the dependency is stated).
2. Trust: only packages shipped with the compiler. Copy-in packages from a project folder are not allowed yet (needs a review rule).
3. `db/` moved too.

## Not done
- Version ranges, a package registry, signatures/hashes of `Rt.cs`, packages depending on packages, interpreter (`aip test`) support.
