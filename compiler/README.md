# aip compiler (minimal)

Front end (`Parser.cs`, `Checker.cs`) is target independent. A back end implements
`IBackend` (`Model.cs`) and turns the checked program into a runnable program.
The only back end so far is `CsBackend.cs`: it emits C# and builds it with the .NET SDK.

    scripts\aip.ps1 check examples\hello
    scripts\aip.ps1 index examples\hello
    scripts\aip.ps1 build examples\hello [--target linux-x64]
    scripts\test.ps1

Entry point: a function named `main`, no parameters, returns Unit. Its `effects`
line lists everything the program can do. Every function must be reachable from `main`.

Standard library: `std/*.fn` are header-only `extern` functions; each back end
supplies the implementation (see `Externs` in `CsBackend.cs`).

Supported so far: `.fn` files; Int, Text, Bool, Unit; `let`, calls, operators,
`if/then/else`, text with `{expr}` parts. Ranges, `requires`, `ensures` and
`example` are parsed and type-checked but not yet proved or run.
