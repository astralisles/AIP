# Where compiler code goes (one topic per file)

The big compiler classes are `partial` and split by topic so parallel branches edit different files.
Add code to the file for its topic; create a new `Class.Topic.cs` rather than growing a shared one.

- Parser: `Parser.cs` (file/signature/body lines), `Parser.Expr.cs` (expressions), `Parser.Tokens.cs` (lexer, token helpers),
  `Parser.TypeFile.cs` (`.type`), `Parser.ConstFile.cs` (`.const`).
- Checker: `Checker.cs` (type helpers, per-function checks, reachability), `Checker.Expr.cs` (type of each expression),
  `Checker.Const.cs` (constants).
- New built-in generic type (`Queue<T>`, ...): one line in `GenericTypes.cs`; the parser and checker both read it.
- New std extern: one `d["name"] = "template";` line in the family file `CsBackend.Externs.<Family>.cs`, and a `case` in
  `Interp.Externs.<Family>.cs` (both for pure functions). A new family is a new pair of files plus one line in
  `BuildExterns` (`CsBackend.Externs.cs`) and one in `ExternFamilies` (`Interp.cs`).
- Generated C# runtime (`Rt`): `CsBackend.Runtime.cs`, one `const string Rt<Topic>` per topic, listed in `RuntimeParts`.
  Each part is emitted as its own generated file (`Rt.<Topic>.cs`).
- `scripts\check-externs.ps1` reads all of these files; keep the `d["name"] = "...";` and `case "name":` shapes.
