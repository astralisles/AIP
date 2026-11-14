# Design decision: native shared libraries (T-0956)

Programs can call functions in shared libraries: `.dll` on Windows, `.so` on Linux, `.dylib` on macOS.

## Syntax

A project function (never `std/`) gets a `native "library" "symbol"` header line and ends with `extern`:

    fn absolute(n I64) -> I64
      "Absolute value, computed by the C runtime library."
      effects native.ucrtbase
      native "ucrtbase" "_abs64"
    extern

- `library` is a bare name, no path and no extension (`sqlite3`, not `/usr/lib/libsqlite3.so`). The .NET loader adds the
  platform prefix and suffix (`sqlite3.dll`, `libsqlite3.so`, `libsqlite3.dylib`) and looks next to the program first.
  The name may differ per OS (Windows `ucrtbase`, Linux `c`); a per-OS choice is a possible follow-up.
- `symbol` is the exported C name; the calling convention is cdecl.
- Types are limited to `I64` (C `int64_t`), `Bool` (one byte), `Text` (UTF-8, NUL-terminated, copied for the call);
  `Unit` as result (`void`). A returned `Text` is copied; the library keeps ownership and AIP never frees it.
  Anything richer is converted by an AIP function wrapped around the native one.

## Security rule (T-0936)

Native code can do anything, so it is an effect, visible in the signature and the index: the function must declare
at least one effect starting with `native.` (convention `native.<library>`), and every caller must declare it too
(the usual effect propagation). `aip test` refuses to run such functions. No `unsafe`, pointers or reflection are
added to the language; the generated C# uses a plain `DllImport`. The old "no FFI" line in `security-T-0936.md`
is superseded by this: FFI exists, but only as this declared effect.

## Dynamic vs static linking

- **Dynamic (implemented):** the library is loaded on the first call. To ship one with the program put the files in
  `<project>/native/`; `aip build` copies every `.dll`, `.so`, `.dylib` into the output folder next to the program.
  Build per OS with `--target` and put the matching library for that OS in `native/`.
- **Static (not implemented):** needs .NET NativeAOT plus a static library on the link line (`DirectPInvoke` and
  `NativeLibrary` items in the csproj). That is a different publish mode and needs a C toolchain per target, so it is
  left for when a native back end exists. The source syntax would not change; only a build option would.

## Errors

`bad-native`, `duplicate-native`, `native-name`, `native-effect`, `native-type`, `native-body`, `extern-no-native`
(an `extern` in a project with no `native` line has no implementation anywhere).

Example: `examples/native` (Windows C runtime; Linux: library `c`, symbols `labs`, `strlen`). Tests: `tests/errors/*native*`, `tests/checks/native.ps1`.
