# The native Windows compiler, written in AIP (T-1033 #2)

Review feedback on #1: the compiler that writes the bytes must itself be AIP code. It now is: `aipc native <projectDir> <out.exe>`
(`examples/aipc/native/`). The C# `--backend native` of #1 stays as a reference; the two are not byte-identical (different
section layout) but print the same output.

Layout, one folder per stage:
- `asm/`: x86-64 instruction encoders (`asmMovRI`, `asmAlu`, `asmJcc`, ...) building `List<I64>` of bytes; register and condition consts.
- `rt/`: the runtime helpers written as machine code (exit, alloc, print, concat, itoa) and the fixed memory layout consts.
- `pe/`: PE32+ writer (headers, KERNEL32 imports) and the linker `natLink`.
- `gen/` (files directly in `native/`): statement reader, checker (`natCheck*`) and code generator (`natGen*`), and `natBuild`.

No labels or mutation in AIP, so: calls and RIP-relative references are 4-item markers `[target+256, kind, 0, 0]` that
`natLink` replaces with 32-bit distances once the layout is known; local jumps use computed lengths; text literals are
inline in `.text`; the PE layout is fixed (image base 0x140000000, .rdata at 0x1000, .bss at 0x2000, .text at 0x4003000)
so data addresses need no fixups.

Supported: I64, Bool, Text, Unit; arithmetic, comparisons, logic, `if` (expression and statement), `let`, text with `{}` parts,
calls, `printLine`, `textFromInt`. Anything else stops with `error native-unsupported: ...` and writes no file.
Next: structs, lists, loops via recursion over lists, then Linux ELF, macOS Mach-O and WASM as further emitters in `native/`.
