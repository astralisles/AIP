# Native back end: machine code and a Windows .exe, no C# (T-1033)

`aip build <project> --backend native` writes a Windows x64 console program byte by byte. There is no C# step,
no .NET SDK at build time and no linker. The exe has no .NET dependency either: it imports only
`GetStdHandle`, `WriteFile` and `ExitProcess` from KERNEL32.

## Pieces (all in `compiler/Aip.Compiler/`)

- `X64.cs`: a small x86-64 assembler (labels, jumps, calls, RIP-relative data references) that emits raw instruction bytes.
- `PeWriter.cs`: lays out the PE32+ file: DOS header, PE headers, `.text`, `.rdata` (import table, then text literals) and
  `.bss` (heap). Fixed image base, so no relocation table. Pure and deterministic.
- `NativeBackend.cs`: `IBackend` that walks the checked program from `main`, generates code for each reachable
  function, adds the run-time helpers (`alloc`, `print`, `concat`, `itoa`, `exit`) in machine code, and calls `PeWriter`.

The front end is unchanged; the back end split is the same one the C# back end uses. Other targets reuse the front end
and the code generator shape, and replace the assembler (ARM64 for macOS/Linux ARM) and the container writer.

## What it compiles today (first slice)

I64, Bool, Text and Unit; functions, calls (also recursive; 256 MB stack reserve), `let`, `if/then/else` (also multi-line Unit),
arithmetic `+ - * / %`, bit `& | ^ ~`, comparisons, `and`/`or`/`not`/`=>` (short-circuit), text with `{expr}` parts
(I64, Bool, Text), `printLine`, `textFromInt`. Division by zero ends the program with exit code 1.
Everything else is rejected at build time with `error native-unsupported` naming the construct; nothing is silently skipped.

Run-time model: calling convention is stack based (arguments pushed left to right, result in `rax`); Text is `[length:8][UTF-8 bytes]`;
allocation is a bump pointer over a 64 MB `.bss` heap with no free (out of memory exits with code 3).

## Not done yet (in rough order)

1. Sized ints, floats, structs, variants and `match`, `List`/`Map`/`Set`, loops, `Fn` references, the rest of the std externs.
2. A garbage collector or region freeing, so long-running programs do not exhaust the heap.
3. Compiling the compiler itself with this back end (the bootstrap, see `bootstrapping.md`): needs 1 and file/process externs.
4. Other targets, each a new container writer plus (for ARM) assembler:
   - Linux x64: ELF64 writer, raw `syscall` instead of KERNEL32 (no libc needed).
   - macOS: Mach-O writer, must be code signed (arm64 requires at least an ad-hoc signature), libSystem or raw syscalls.
   - WASM: a module writer (type, function, code sections) with a WASI `fd_write` import; the stack machine shape maps directly.
   The native back end refuses these targets today with `unsupported-target`.
