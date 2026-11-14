using System.Text;

namespace Aip;

/// <summary>Back end that writes machine code and a Windows .exe directly: no C#, no .NET SDK, no linker.
/// First slice (docs/design/native-backend-T-1033.md): I64, Bool, Text, Unit; calls, `let`, `if`, arithmetic,
/// comparisons, `and`/`or`/`not`, text with `{expr}` parts, `printLine` and `textFromInt`. Anything else is
/// the error `native-unsupported`. Windows x64 only; macOS, Linux and WASM are separate emitters on the same X64/PE split.</summary>
sealed class NativeBackend : IBackend
{
    public Action<string> Log { get; init; } = _ => { };
    public string Name => "native";

    public string? Build(ProgramModel program, string outDir, string target)
    {
        if (target != "windows-x64")
        {
            Console.WriteLine($"error unsupported-target: the native back end cannot build for `{target}` yet\n  fix: use --target windows-x64, or drop --backend native to use the cs back end");
            return null;
        }
        byte[] exe;
        try { exe = Compile(program); }
        catch (CompileException ex) { Console.WriteLine(ex.Diagnostic); return null; }
        var path = Path.Combine(outDir, "bin", "native-windows-x64", program.Name + ".exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, exe);
        Log($"wrote {exe.Length} bytes of machine code and headers to {path}");
        return path;
    }

    /// <summary>The whole .exe as bytes. Pure and deterministic: used by the build and by tests.</summary>
    public static byte[] Compile(ProgramModel program)
    {
        var g = new Gen(program);
        g.Run();
        return PeWriter.Build(g.Asm, g.Literals.ToArray());
    }

    sealed class Gen(ProgramModel program)
    {
        public readonly X64 Asm = new();
        public readonly List<byte> Literals = new();
        readonly Dictionary<string, int> _lits = new(StringComparer.Ordinal);
        readonly Dictionary<string, int> _fnLabels = new(StringComparer.Ordinal);
        readonly Queue<Fn> _todo = new();
        readonly Dictionary<string, int> _slots = new(StringComparer.Ordinal);
        Fn _fn = null!;
        int _alloc, _print, _concat, _itoa, _exit;
        X64 A => Asm;

        static bool Supported(string t) => t is "I64" or "Bool" or "Text" or "Unit" or "";

        CompileException Unsupported(string what, int line)
            => new(new Diagnostic(_fn?.File ?? program.Dir, line, "native-unsupported", $"the native back end cannot compile {what} yet",
                "build with the default cs back end (drop --backend native), or rewrite using I64, Bool, Text, calls, let, if and printLine"));

        public void Run()
        {
            _alloc = A.NewLabel(); _print = A.NewLabel(); _concat = A.NewLabel(); _itoa = A.NewLabel(); _exit = A.NewLabel();
            var main = FnLabel("main");
            // Entry: set up the heap, run main, exit(0).
            A.LeaRip(X64.Rax, Seg.Bss, PeWriter.BssHeap);
            A.StoreRip(Seg.Bss, PeWriter.BssHeapPtr, X64.Rax);
            A.AluRI(0, X64.Rax, PeWriter.HeapSize);
            A.StoreRip(Seg.Bss, PeWriter.BssHeapEnd, X64.Rax);
            A.Call(main);
            A.MovRI(X64.Rcx, 0);
            A.Call(_exit);
            while (_todo.Count > 0) EmitFn(_todo.Dequeue());
            EmitRuntime();
            A.ResolveLabels();
        }

        int FnLabel(string name)
        {
            if (_fnLabels.TryGetValue(name, out var l)) return l;
            l = A.NewLabel();
            _fnLabels[name] = l;
            _todo.Enqueue(program.Fns[name]);
            return l;
        }

        /// <summary>A text literal in .rdata: 8-byte length, the UTF-8 bytes, padding to 8.</summary>
        void LeaLiteral(string s)
        {
            if (!_lits.TryGetValue(s, out var off))
            {
                off = Literals.Count;
                var bytes = Encoding.UTF8.GetBytes(s);
                for (int i = 0; i < 8; i++) Literals.Add((byte)((long)bytes.Length >> (8 * i)));
                Literals.AddRange(bytes);
                while (Literals.Count % 8 != 0) Literals.Add(0);
                _lits[s] = off;
            }
            A.LeaRip(X64.Rax, Seg.Rdata, PeWriter.ImportSize + off);
        }

        // ---- functions ----

        void EmitFn(Fn fn)
        {
            _fn = fn;
            if (fn.IsExtern || fn.IsConst || fn.Body.Count == 0)
                throw Unsupported($"the std function `{fn.Name}`", fn.Line);
            if (!Supported(fn.Ret.Name) || fn.Params.Any(p => !Supported(p.Type.Name)))
                throw Unsupported($"the types in `{fn.Signature}`", fn.Line);
            _slots.Clear();
            int n = fn.Params.Count;
            for (int i = 0; i < n; i++) _slots[fn.Params[i].Name] = 16 + 8 * (n - 1 - i); // caller pushed the arguments left to right
            int lets = fn.Body.Count(s => s.Let != null);
            A.Bind(_fnLabels[fn.Name]);
            A.Push(X64.Rbp);
            A.MovRR(X64.Rbp, X64.Rsp);
            if (lets > 0) A.AluRI(5, X64.Rsp, 8 * lets);
            int k = 0;
            foreach (var s in fn.Body)
            {
                Gen_(s.Value);
                if (s.Let != null)
                {
                    if (!Supported(s.Value.Type)) throw Unsupported($"a `let` of type {s.Value.Type}", s.Line);
                    _slots[s.Let] = -8 * ++k;
                    A.Store(X64.Rbp, _slots[s.Let], X64.Rax);
                }
            }
            A.Leave();
            A.Ret();
        }

        // ---- expressions: the value ends up in rax (Text is a pointer to length + bytes) ----

        void Gen_(Expr e)
        {
            switch (e)
            {
                case IntLit i when i.Type is "I64" or "":
                    A.MovRI(X64.Rax, unchecked((long)i.Value));
                    break;
                case BoolLit b:
                    A.MovRI(X64.Rax, b.Value ? 1 : 0);
                    break;
                case TextLit t:
                    GenText(t);
                    break;
                case NameRef { Const: false, Erased: false } n when _slots.TryGetValue(n.Name, out var disp):
                    A.Load(X64.Rax, X64.Rbp, disp);
                    break;
                case Unary u when u.Operand.Type is "I64" or "" && u.Op is "-" or "~":
                    Gen_(u.Operand);
                    A.GroupF7(u.Op == "-" ? 3 : 2, X64.Rax);
                    break;
                case Unary { Op: "not" } u:
                    Gen_(u.Operand);
                    A.AluRI(6, X64.Rax, 1);
                    break;
                case Binary b:
                    GenBinary(b);
                    break;
                case IfExpr f:
                    {
                        int els = A.NewLabel(), end = A.NewLabel();
                        Gen_(f.Cond);
                        A.Test(X64.Rax, X64.Rax);
                        A.Jcc(X64.CcE, els);
                        Gen_(f.Then);
                        A.Jmp(end);
                        A.Bind(els);
                        Gen_(f.Else);
                        A.Bind(end);
                        break;
                    }
                case BlockExpr k:
                    foreach (var s in k.Stmts) Gen_(s);
                    break;
                case Call { Ctor: null, Indirect: false, Erased: false } c:
                    GenCall(c);
                    break;
                default:
                    throw Unsupported(e.GetType().Name switch { "IntLit" => $"an {e.Type} number", "NameRef" => "this name", var n => "`" + n + "` expressions" }, e.Line);
            }
        }

        void GenText(TextLit t)
        {
            if (t.Parts.Count == 0) { LeaLiteral(""); return; }
            for (int i = 0; i < t.Parts.Count; i++)
            {
                if (t.Parts[i] is string s) LeaLiteral(s);
                else
                {
                    var p = (Expr)t.Parts[i];
                    Gen_(p);
                    switch (p.Type)
                    {
                        case "Text": break;
                        case "I64" or "": A.Call(_itoa); break;
                        case "Bool":
                            {
                                int no = A.NewLabel(), end = A.NewLabel();
                                A.Test(X64.Rax, X64.Rax);
                                A.Jcc(X64.CcE, no);
                                LeaLiteral("true"); A.Jmp(end);
                                A.Bind(no); LeaLiteral("false");
                                A.Bind(end);
                                break;
                            }
                        default: throw Unsupported($"a {p.Type} inside text", p.Line);
                    }
                }
                if (i > 0)
                {
                    A.MovRR(X64.Rdx, X64.Rax);
                    A.Pop(X64.Rcx);
                    A.Call(_concat);
                }
                if (i < t.Parts.Count - 1) A.Push(X64.Rax);
            }
        }

        void GenBinary(Binary b)
        {
            if (b.Op is "and" or "or" or "=>")
            {
                int end = A.NewLabel();
                Gen_(b.Left);
                if (b.Op == "=>") A.AluRI(6, X64.Rax, 1); // a => b is (not a) or b
                A.Test(X64.Rax, X64.Rax);
                A.Jcc(b.Op == "and" ? X64.CcE : X64.CcNe, end);
                Gen_(b.Right);
                A.Bind(end);
                return;
            }
            var lt = b.Left.Type;
            bool ints = lt is "I64" or "";
            if (!ints && !(lt == "Bool" && b.Op is "==" or "!=")) throw Unsupported($"`{b.Op}` on {lt}", b.Line);
            Gen_(b.Left);
            A.Push(X64.Rax);
            Gen_(b.Right);
            A.MovRR(X64.Rcx, X64.Rax);
            A.Pop(X64.Rax);
            switch (b.Op)
            {
                case "+": A.Alu(X64.Add, X64.Rax, X64.Rcx); break;
                case "-": A.Alu(X64.Sub, X64.Rax, X64.Rcx); break;
                case "&": A.Alu(X64.And, X64.Rax, X64.Rcx); break;
                case "|": A.Alu(X64.Or, X64.Rax, X64.Rcx); break;
                case "^": A.Alu(X64.Xor, X64.Rax, X64.Rcx); break;
                case "*": A.Imul(X64.Rax, X64.Rcx); break;
                case "/" or "%":
                    {
                        int ok = A.NewLabel();
                        A.Test(X64.Rcx, X64.Rcx);
                        A.Jcc(X64.CcNe, ok);
                        A.MovRI(X64.Rcx, 1);          // division by zero ends the program with exit code 1
                        A.Call(_exit);
                        A.Bind(ok);
                        A.Cqo();
                        A.GroupF7(7, X64.Rcx);
                        if (b.Op == "%") A.MovRR(X64.Rax, X64.Rdx);
                        break;
                    }
                case "==" or "!=" or "<" or "<=" or ">" or ">=":
                    A.Alu(X64.Cmp, X64.Rax, X64.Rcx);
                    A.SetCc(b.Op switch { "==" => X64.CcE, "!=" => X64.CcNe, "<" => X64.CcL, "<=" => X64.CcLe, ">" => X64.CcG, _ => X64.CcGe });
                    A.MovzxAl();
                    break;
                default: throw Unsupported($"the operator `{b.Op}`", b.Line);
            }
        }

        void GenCall(Call c)
        {
            switch (c.Name)
            {
                case "printLine" when c.Args.Count == 1:
                    Gen_(c.Args[0]);
                    A.MovRR(X64.Rcx, X64.Rax);
                    A.Call(_print);
                    return;
                case "textFromInt" when c.Args.Count == 1:
                    Gen_(c.Args[0]);
                    A.Call(_itoa);
                    return;
            }
            if (!program.Fns.TryGetValue(c.Name, out var callee) || callee.IsExtern || callee.IsConst || callee.Body.Count == 0)
                throw Unsupported($"the call to `{c.Name}`", c.Line);
            var label = FnLabel(c.Name);
            foreach (var arg in c.Args) { Gen_(arg); A.Push(X64.Rax); }
            A.Call(label);
            if (c.Args.Count > 0) A.AluRI(0, X64.Rsp, 8 * c.Args.Count);
        }

        // ---- run-time helpers, written directly in machine code ----

        void EmitRuntime()
        {
            // exit(rcx): ExitProcess with a 16-byte aligned stack and shadow space.
            A.Bind(_exit);
            A.AluRI(4, X64.Rsp, -16);
            A.AluRI(5, X64.Rsp, 32);
            A.CallRip(Seg.Rdata, PeWriter.IatSlot(PeWriter.ExitProcessSlot));

            // alloc(rcx bytes) -> rax: bump allocator over the .bss heap; nothing is freed. Out of memory ends the program with exit code 3.
            A.Bind(_alloc);
            int oom = A.NewLabel();
            A.AluRI(0, X64.Rcx, 7);
            A.AluRI(4, X64.Rcx, -8);
            A.LoadRip(X64.Rax, Seg.Bss, PeWriter.BssHeapPtr);
            A.MovRR(X64.Rdx, X64.Rax);
            A.Alu(X64.Add, X64.Rdx, X64.Rcx);
            A.LoadRip(X64.R10, Seg.Bss, PeWriter.BssHeapEnd);
            A.Alu(X64.Cmp, X64.Rdx, X64.R10);
            A.Jcc(X64.CcA, oom);
            A.StoreRip(Seg.Bss, PeWriter.BssHeapPtr, X64.Rdx);
            A.Ret();
            A.Bind(oom);
            A.MovRI(X64.Rcx, 3);
            A.Jmp(_exit);

            // print(rcx text): the text and a newline to standard output.
            A.Bind(_print);
            A.Push(X64.Rbp); A.MovRR(X64.Rbp, X64.Rsp); A.Push(X64.Rbx); A.Push(X64.Rsi);
            A.MovRR(X64.Rbx, X64.Rcx);
            A.AluRI(4, X64.Rsp, -16);
            A.AluRI(5, X64.Rsp, 64);
            A.MovRI(X64.Rcx, -11);                 // STD_OUTPUT_HANDLE
            A.CallRip(Seg.Rdata, PeWriter.IatSlot(PeWriter.StdHandleSlot));
            A.MovRR(X64.Rsi, X64.Rax);
            A.MovRR(X64.Rcx, X64.Rsi);
            A.Lea(X64.Rdx, X64.Rbx, 8);
            A.Load(X64.R8, X64.Rbx, 0);
            A.Lea(X64.R9, X64.Rsp, 40);
            A.MovRI(X64.Rax, 0);
            A.Store(X64.Rsp, 32, X64.Rax);
            A.CallRip(Seg.Rdata, PeWriter.IatSlot(PeWriter.WriteFileSlot));
            A.MovRI(X64.Rax, 10);
            A.StoreByte(X64.Rsp, 48, X64.Rax);
            A.MovRR(X64.Rcx, X64.Rsi);
            A.Lea(X64.Rdx, X64.Rsp, 48);
            A.MovRI(X64.R8, 1);
            A.Lea(X64.R9, X64.Rsp, 40);
            A.MovRI(X64.Rax, 0);
            A.Store(X64.Rsp, 32, X64.Rax);
            A.CallRip(Seg.Rdata, PeWriter.IatSlot(PeWriter.WriteFileSlot));
            A.Lea(X64.Rsp, X64.Rbp, -16);
            A.Pop(X64.Rsi); A.Pop(X64.Rbx); A.Pop(X64.Rbp);
            A.Ret();

            // concat(rcx a, rdx b) -> rax: a new text holding a then b.
            A.Bind(_concat);
            A.MovRR(X64.R8, X64.Rcx);
            A.MovRR(X64.R9, X64.Rdx);
            A.Load(X64.R11, X64.R8, 0);
            A.Load(X64.R10, X64.R9, 0);
            A.Alu(X64.Add, X64.R11, X64.R10);
            A.Lea(X64.Rcx, X64.R11, 8);
            A.Call(_alloc);
            A.Store(X64.Rax, 0, X64.R11);
            A.Lea(X64.Rdi, X64.Rax, 8);
            A.Lea(X64.Rsi, X64.R8, 8);
            A.Load(X64.Rcx, X64.R8, 0);
            A.RepMovsb();
            A.Lea(X64.Rsi, X64.R9, 8);
            A.Load(X64.Rcx, X64.R9, 0);
            A.RepMovsb();
            A.Ret();

            // itoa(rax n) -> rax: n in decimal as a text. Digits are written backwards from the end of a 32-byte block.
            A.Bind(_itoa);
            int pos = A.NewLabel(), nonNeg = A.NewLabel(), loop = A.NewLabel();
            A.Push(X64.Rbp); A.MovRR(X64.Rbp, X64.Rsp);
            A.MovRR(X64.Rsi, X64.Rax);
            A.MovRR(X64.R8, X64.Rax);
            A.MovRI(X64.Rcx, 32);
            A.Call(_alloc);
            A.MovRR(X64.R11, X64.Rax);
            A.Lea(X64.Rdi, X64.Rax, 32);
            A.MovRI(X64.R9, 10);
            A.Test(X64.Rsi, X64.Rsi);
            A.Jcc(X64.CcNs, nonNeg);
            A.GroupF7(3, X64.Rsi);
            A.Bind(nonNeg);
            A.Bind(loop);
            A.MovRR(X64.Rax, X64.Rsi);
            A.Alu(X64.Xor, X64.Rdx, X64.Rdx);
            A.GroupF7(6, X64.R9);                  // unsigned: also right for the most negative number
            A.MovRR(X64.Rsi, X64.Rax);
            A.AluRI(0, X64.Rdx, '0');
            A.AluRI(5, X64.Rdi, 1);
            A.StoreByte(X64.Rdi, 0, X64.Rdx);
            A.Test(X64.Rsi, X64.Rsi);
            A.Jcc(X64.CcNe, loop);
            A.Test(X64.R8, X64.R8);
            A.Jcc(X64.CcNs, pos);
            A.AluRI(5, X64.Rdi, 1);
            A.MovRI(X64.Rdx, '-');
            A.StoreByte(X64.Rdi, 0, X64.Rdx);
            A.Bind(pos);
            A.Lea(X64.Rax, X64.R11, 32);
            A.Alu(X64.Sub, X64.Rax, X64.Rdi);      // length
            A.AluRI(5, X64.Rdi, 8);
            A.Store(X64.Rdi, 0, X64.Rax);
            A.MovRR(X64.Rax, X64.Rdi);
            A.Pop(X64.Rbp);
            A.Ret();
        }
    }
}
