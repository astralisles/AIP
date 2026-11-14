# Where the assembler and the AIP compiler live (T-1035)

Done: the compiler written in AIP moved from `examples/aipc` to `compiler/aipc`. It is not an example any more
(no `expected-output.txt`); the bootstrap scripts and checks build it from the new path.

Open: the x86-64 assembler (`compiler/aipc/native/asm`, 59 files) should sit beside `std` as a shared library.
A project only sees its own folder, `std` and declared packages, so this needs a rule for how a project names a
source library (for example a `libs.txt` like `packages.txt`, loaded like std but with user-style names and checks).
Not built yet: it is a language/loader decision.
