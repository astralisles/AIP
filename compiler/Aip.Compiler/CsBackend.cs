using System.Diagnostics;
using System.Text;

namespace Aip;

/// <summary>Back end that emits C# and builds it with the .NET SDK.</summary>
sealed partial class CsBackend : IBackend
{
    public Action<string> Log { get; init; } = _ => { };
    public string Name => "cs";

    /// <summary>Language target name -> .NET runtime identifier.</summary>
    static readonly Dictionary<string, string> Targets = new()
    {
        ["windows-x64"] = "win-x64",
        ["windows-arm64"] = "win-arm64",
        ["linux-x64"] = "linux-x64",
        ["linux-arm64"] = "linux-arm64",
        ["macos-arm64"] = "osx-arm64",
    };

    public string? Build(ProgramModel program, string outDir, string target)
    {
        if (!Targets.TryGetValue(target, out var rid))
        {
            Console.WriteLine($"error unsupported-target: the cs back end cannot build for `{target}` yet\n  fix: use one of: {string.Join(", ", Targets.Keys)}");
            return null;
        }
        // Only externs the program can reach need an implementation: the std library may declare functions this back end does not have yet.
        var reach = new HashSet<string>();
        var todo = new Stack<string>(program.Fns.Keys.Where(k => k == "main"));
        while (todo.Count > 0)
        {
            var name = todo.Pop();
            if (reach.Add(name) && program.Fns.TryGetValue(name, out var cur)) foreach (var callee in cur.Calls) todo.Push(callee);
        }
        foreach (var fn in program.Fns.Values.Where(f => f.IsExtern && f.NativeLib == null && !Externs.ContainsKey(f.Name) && reach.Contains(f.Name)))
        {
            Console.WriteLine($"error backend-missing-extern: the cs back end has no implementation of `{fn.Name}`");
            return null;
        }

        var srcDir = Path.Combine(outDir, "cs");
        var csproj = Path.Combine(srcDir, program.Name + ".csproj");
        var binDir = Path.Combine(outDir, "bin", target);
        var exePath = Path.Combine(binDir, program.Name + (target.StartsWith("windows", StringComparison.Ordinal) ? ".exe" : ""));

        // Speed: the slow part is `dotnet publish` (~2.5 s). Skip it when the generated C# is identical
        // to the last build for this target and its output is still there; otherwise keep the cs/obj
        // folder so the publish is incremental instead of restoring from scratch.
        var files = EmitFiles(program);
        CopyNative(program, binDir);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(rid + "\0" + string.Join("\0", files.Select(p => p.Key + "\0" + p.Value)))));
        var stamp = Path.Combine(binDir, ".aip-build-hash");
        if (File.Exists(exePath) && File.Exists(stamp) && File.ReadAllText(stamp) == hash)
        {
            Log("generated C# unchanged since last build; skipping dotnet publish");
            return exePath;
        }
        Log($"generated {files.Count} C# file(s) in {srcDir}; running dotnet publish");

        Directory.CreateDirectory(srcDir);
        foreach (var old in Directory.EnumerateFiles(srcDir, "*.cs").Where(p => !files.ContainsKey(Path.GetFileName(p))))
            File.Delete(old);
        foreach (var (name, content) in files)
        {
            var path = Path.Combine(srcDir, name);
            if (!File.Exists(path) || File.ReadAllText(path) != content) File.WriteAllText(path, content);
        }
        if (File.Exists(stamp)) File.Delete(stamp);
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in new[] { "publish", csproj, "-c", "Release", "-r", rid, "--self-contained", "false", "-o", binDir, "-nologo", "-v", "q" })
            psi.ArgumentList.Add(a);
        psi.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        using var proc = Process.Start(psi)!;
        var errTask = proc.StandardError.ReadToEndAsync();
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            Log($"dotnet publish exit code {proc.ExitCode}");
            Console.WriteLine("error backend-failed: the generated C# did not build (this is a compiler bug or a missing .NET component)");
            foreach (var line in (output + errTask.Result).Split('\n').Where(l => l.Contains("error")).Distinct().Take(10))
                Console.WriteLine("  " + line.Trim());
            return null;
        }
        File.WriteAllText(stamp, hash);
        return exePath;
    }

    /// <summary>Shared libraries shipped with the project: every .dll, .so and .dylib in `<project>/native/`
    /// is copied next to the program, where the loader looks first.</summary>
    static void CopyNative(ProgramModel program, string binDir)
    {
        var src = Path.Combine(program.Dir, "native");
        if (!Directory.Exists(src)) return;
        Directory.CreateDirectory(binDir);
        foreach (var f in Directory.EnumerateFiles(src).Where(f => Path.GetExtension(f) is ".dll" or ".so" or ".dylib" || Path.GetFileName(f).Contains(".so.", StringComparison.Ordinal)))
        {
            var dest = Path.Combine(binDir, Path.GetFileName(f));
            if (!File.Exists(dest) || File.GetLastWriteTimeUtc(dest) != File.GetLastWriteTimeUtc(f)) File.Copy(f, dest, true);
        }
    }

    /// <summary>All generated files (name to content), in a fixed order. Pure: used by the build and by `aip emit`.</summary>
    public static SortedDictionary<string, string> EmitFiles(ProgramModel program)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        // Std constants (numPi, ...) are emitted too; they are lazy, so an unused one costs nothing at run time.
        foreach (var fn in program.ProjectFns.Concat(program.Fns.Values.Where(f => f.IsStd && f.IsConst).OrderBy(f => f.Name, StringComparer.Ordinal))) files[fn.Name + ".cs"] = fn.IsConst ? EmitConst(fn) : fn.NativeLib != null ? EmitNative(fn) : EmitFn(fn);
        files["Program.cs"] = ProgramCs;
        files["Persistent.cs"] = PersistentRt.Source;
        foreach (var (name, source) in RuntimeParts) files[name] = source;
        if (program.Types.Count > 0) files["Types.cs"] = EmitTypes(program);
        if (UsesDb(program)) files["Rt.Db.cs"] = RtDb;
        if (UsesMongo(program)) files["Rt.Mongo.cs"] = RtMongo;
        files[program.Name + ".csproj"] = CsprojWithPackages(UsesDb(program), UsesMongo(program));
        return files;
    }

    /// <summary>The names of the standard-library functions the back end implements.</summary>
    public static IEnumerable<string> ExternNames => Externs.Keys.OrderBy(k => k, StringComparer.Ordinal);

    // Lists are type-erased at run time (List<object>); reads cast back to the element type.
    static string CsType(string t) => t switch
    {
        "Int" => "long", "F32" => "float", "F64" => "double", "I8" => "sbyte", "I16" => "short", "I32" => "int",
        "U8" => "byte", "U16" => "ushort", "U32" => "uint", "U64" => "ulong", "Text" => "string", "Bool" => "bool", "_" => "object",
        "TcpListener" or "TcpConn" or "UdpSocket" or "DbConn" or "MongoConn" => "object", // handles, opened by effectful std functions
        _ when t.Length == 1 && char.IsAsciiLetterUpper(t[0]) => "object", // a type parameter: erased
        _ when t.StartsWith("List<", StringComparison.Ordinal) => "Aip.Persistent.PList",
        _ when t.StartsWith("Queue<", StringComparison.Ordinal) => "Aip.Persistent.PFifo",
        _ when t.StartsWith("PriorityQueue<", StringComparison.Ordinal) => "Aip.Persistent.PQueue",
        // All collections are persistent (PersistentRt.cs): an update shares nearly all memory with the old value. A set maps each item to true.
        _ when t.StartsWith("Map<", StringComparison.Ordinal) || t.StartsWith("Set<", StringComparison.Ordinal) => "Aip.Persistent.PMap",
        _ when t.StartsWith("SortedMap<", StringComparison.Ordinal) || t.StartsWith("SortedSet<", StringComparison.Ordinal) => "Aip.Persistent.PSorted",
        "Unit" => "void",
        _ when t.IndexOf('<') is var lt and > 0 => t[..lt], // a generic struct or variant: one erased record
        _ => t, // a struct or variant: a C# record of the same name (see EmitTypes)
    };

    static string Fields(Case c) => string.Join(", ", c.Fields.Select(f => $"{CsType(f.Type.Name)} @{f.Name}"));

    /// <summary>A struct is a sealed record; a variant is an abstract record with one nested sealed record per case.</summary>
    static string EmitTypes(ProgramModel program)
    {
        var sb = new StringBuilder("// Generated from the .struct and .variant files. Do not edit.\n");
        foreach (var def in program.Types.Values.OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"/// <summary>{System.Security.SecurityElement.Escape(def.Doc)}</summary>\n");
            if (!def.IsVariant)
            {
                // Fields are settable (`set p.f = v`): a struct is a reference, so every holder sees the change.
                var body = string.Concat(def.Cases[0].Fields.Select(f => $" public {CsType(f.Type.Name)} @{f.Name} {{ get; set; }} = @{f.Name};"));
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"public sealed record @{def.Name}({Fields(def.Cases[0])}) {{{body} }}\n");
            }
            else
            {
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"public abstract record @{def.Name}\n{{\n    private @{def.Name}() {{ }}\n");
                foreach (var c in def.Cases) sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"    public sealed record @{c.Name}({Fields(c)}) : @{def.Name};\n");
                sb.Append("}\n");
            }
        }
        return sb.ToString();
    }

    static string Pattern(Arm a) => a.Binds.Count == 0 ? $"{a.Path} _" : $"{a.Path}({string.Join(", ", a.Binds.Select(b => $"var @{b}"))})";

    /// <summary>A `native "lib" "symbol"` function: a P/Invoke into a shared library (dll / so / dylib), loaded on first call.
    /// Int is a 64-bit integer, Bool one byte, Text a UTF-8 C string; a returned string stays owned by the library.</summary>
    static string EmitNative(Fn fn)
    {
        string Raw(string t, bool ret) => t switch
        {
            "Int" => "long", "Bool" => "bool", "Unit" => "void", _ => ret ? "System.IntPtr" : "string",
        };
        string Attr(string t) => t switch
        {
            "Text" => "[System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] ",
            "Bool" => "[System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U1)] ",
            _ => "",
        };
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(ci, $"// Generated from {fn.Name}.fn. Do not edit.\n");
        sb.Append("public static partial class Fn\n{\n");
        sb.Append(ci, $"    [System.Runtime.InteropServices.DllImport({Quote(fn.NativeLib!)}, EntryPoint = {Quote(fn.NativeSymbol)}, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]\n");
        if (fn.Ret.Name == "Bool") sb.Append("    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U1)]\n");
        sb.Append(ci, $"    static extern {Raw(fn.Ret.Name, true)} Native_{fn.Name}({string.Join(", ", fn.Params.Select(p => $"{Attr(p.Type.Name)}{Raw(p.Type.Name, false)} @{p.Name}"))});\n\n");
        sb.Append(ci, $"    /// <summary>{System.Security.SecurityElement.Escape(fn.Doc)}</summary>\n");
        sb.Append(ci, $"    public static {CsType(fn.Ret.Name)} @{fn.Name}({string.Join(", ", fn.Params.Select(p => $"{CsType(p.Type.Name)} @{p.Name}"))})\n    {{\n");
        var call = $"Native_{fn.Name}({string.Join(", ", fn.Params.Select(p => $"@{p.Name}"))})";
        sb.Append(fn.Ret.Name switch
        {
            "Text" => $"        return System.Runtime.InteropServices.Marshal.PtrToStringUTF8({call}) ?? \"\";\n",
            "Unit" => $"        {call};\n",
            _ => $"        return {call};\n",
        });
        sb.Append("    }\n}\n");
        return sb.ToString();
    }

    static string EmitFn(Fn fn)
    {
        var sb = new StringBuilder();
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"// Generated from {fn.Name}.fn. Do not edit.\n");
        sb.Append("public static partial class Fn\n{\n");
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"    /// <summary>{System.Security.SecurityElement.Escape(fn.Doc)}</summary>\n");
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"    public static {CsType(fn.Ret.Name)} @{fn.Name}({string.Join(", ", fn.Params.Select(p => $"{CsType(p.Type.Name)} @{p.Name}"))})\n    {{\n");
        // Opt-in run-time inspection (AIP_PROFILE / AIP_CALLS, see compiler/README.md): off by default, one bool check per call.
        var argList = string.Join(", ", fn.Params.Select(p => $"@{p.Name}"));
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"        if (Rt.Calls) Rt.Call(\"{fn.Name}\", new object[] {{ {argList} }});\n");
        sb.Append("        long __t = Rt.Prof ? Rt.Enter() : 0;\n        try\n        {\n");
        for (int i = 0; i < fn.Body.Count; i++)
        {
            var s = fn.Body[i];
            if (s.Let != null) sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"        var @{s.Let} = {Emit(s.Value)};\n");
            else if (i == fn.Body.Count - 1 && fn.Ret.Name != "Unit") sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"        return {Emit(s.Value)};\n");
            else sb.Append(EmitUnit(s.Value, "        "));
        }
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"        }}\n        finally {{ if (Rt.Prof) Rt.Leave(\"{fn.Name}\", __t); }}\n");
        sb.Append("    }\n}\n");
        return sb.ToString();
    }

    /// <summary>A `.const` file: a property computed once, on first use. Lazy (not a static initializer) so the
    /// order of constants across files does not matter; the checker rejects cycles, and the value is pure.</summary>
    static string EmitConst(Fn k)
    {
        var t = CsType(k.Ret.Name);
        var sb = new StringBuilder();
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"// Generated from {k.Name}.const. Do not edit.\n");
        sb.Append("public static partial class Const\n{\n");
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"    /// <summary>{System.Security.SecurityElement.Escape(k.Doc)}</summary>\n");
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"    public static {t} @{k.Name} => ConstCell.@{k.Name}.Value;\n}}\n");
        sb.Append("static partial class ConstCell\n{\n");
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"    internal static readonly System.Lazy<{t}> @{k.Name} = new(() => {Emit(k.Body[0].Value)});\n}}\n");
        return sb.ToString();
    }

    /// <summary>A statement. A Unit `if` becomes an if statement because C# cannot use void in `?:`.</summary>
    static string EmitUnit(Expr e, string indent) => e switch
    {
        IfExpr { Type: "Unit" } i =>
            $"{indent}if ({Emit(i.Cond)})\n{indent}{{\n{EmitUnit(i.Then, indent + "    ")}{indent}}}\n{indent}else\n{indent}{{\n{EmitUnit(i.Else, indent + "    ")}{indent}}}\n",
        BlockExpr k => string.Concat(k.Stmts.Select(s => EmitUnit(s, indent))),
        MatchExpr { Type: "Unit" } m =>
            $"{indent}switch ({Emit(m.Subject)})\n{indent}{{\n"
            + string.Concat(m.Arms.Select(a => $"{indent}    case {Pattern(a)}:\n{EmitUnit(a.Body, indent + "        ")}{indent}        break;\n"))
            + $"{indent}    default: throw new System.InvalidOperationException();\n{indent}}}\n",
        Assign a => $"{indent}{Emit(a.Target.Target)}.@{a.Target.Name} = {Emit(a.Value)};\n",
        _ => $"{indent}{Emit(e)};\n",
    };

    static string Emit(Expr e) => e switch
    {
        IntLit { Type: "U64" } i => i.Value + "UL",
        IntLit { Type: "Int" or "" } i => i.Value + "L",
        IntLit i => $"(({CsType(i.Type)}){i.Value})",
        Unary { Op: "-", Type: not ("Int" or "" or "F32" or "F64") } u when u.Operand is IntLit n => $"(({CsType(u.Type)})(-{n.Value}))",
        Unary { Op: "-", Type: not ("Int" or "" or "F32" or "F64") } u => $"checked(({CsType(u.Type)})(-{Emit(u.Operand)}))",
        FloatLit { Type: "F32" } f => f.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "F",
        FloatLit f => f.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "D",
        BoolLit b => b.Value ? "true" : "false",
        NameRef n => n.Const ? "Const.@" + n.Name : Cast(n.Erased, n.Type, "@" + n.Name),
        TextLit t => t.Parts.Count == 1 && t.Parts[0] is string only
            ? Quote(only)
            : "string.Concat(" + string.Join(", ", t.Parts.Select(p => p is string s ? Quote(s) : $"Rt.Text({Emit((Expr)p)})")) + ")",
        Call { Ctor: not null } c => $"new {c.Ctor}({string.Join(", ", c.Args.Select(Emit))})",
        Field f => Cast(f.Erased, f.Type, $"{Emit(f.Target)}.@{f.Name}"),
        MatchExpr m => $"({Emit(m.Subject)} switch {{ {string.Join(", ", m.Arms.Select(a => $"{Pattern(a)} => {Emit(a.Body)}"))}, _ => throw new System.InvalidOperationException() }})",
        Call c => Externs.TryGetValue(c.Name, out var template)
            ? (c.Name is "listGet" or "mapGet" or "sortedMapGet" or "queueFront" or "priorityQueueFront" ? $"(({CsType(c.Type)})" : "") + string.Format(System.Globalization.CultureInfo.InvariantCulture, template.Replace("<T>", CsType(c.Type)), c.Args.Select(Emit).ToArray<object>()) + (c.Name is "listGet" or "mapGet" or "sortedMapGet" or "queueFront" or "priorityQueueFront" ? ")" : "")
            : Cast(c.Erased, c.Type, $"Fn.@{c.Name}({string.Join(", ", c.Args.Select(Emit))})"),
        Unary u => u.Op == "-" ? $"(-{Emit(u.Operand)})" : u.Op == "~" ? $"unchecked(({CsType(u.Type)})(~{Emit(u.Operand)}))" : $"(!{Emit(u.Operand)})",
        Binary b => b.Op switch
        {
            "and" => $"({Emit(b.Left)} && {Emit(b.Right)})",
            "or" => $"({Emit(b.Left)} || {Emit(b.Right)})",
            "=>" => $"(!{Emit(b.Left)} || {Emit(b.Right)})",
            // Bit operations: C# widens small types to int, so cast back. Shift counts outside 0..width-1 are errors.
            "&" or "|" or "^" => $"unchecked(({CsType(b.Type)})({Emit(b.Left)} {b.Op} {Emit(b.Right)}))",
            "<<" or ">>" => $"unchecked(({CsType(b.Type)})({Emit(b.Left)} {b.Op} Rt.Shift({Emit(b.Right)}, sizeof({CsType(b.Type)}) * 8)))",
            // Sized integers: C# widens to int, so cast back; `checked` makes overflow a run-time error, never a silent wrap.
            "+" or "-" or "*" or "/" or "%" when b.Type is not ("Int" or "" or "F32" or "F64") =>
                $"checked(({CsType(b.Type)})({Emit(b.Left)} {b.Op} {Emit(b.Right)}))",
            _ => $"({Emit(b.Left)} {b.Op} {Emit(b.Right)})",
        },
        IfExpr i => $"({Emit(i.Cond)} ? {Emit(i.Then)} : {Emit(i.Else)})",
        _ => throw new InvalidOperationException("unknown expression"),
    };

    /// <summary>A value that is `object` in C# because its declared type is a type parameter is cast back to its real type.</summary>
    static string Cast(bool erased, string type, string code) => erased && type != "" && CsType(type) != "object" ? $"(({CsType(type)}){code})" : code;

    static string Quote(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
            sb.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\t' => "\\t",
                _ when c < ' ' || c > '~' => $"\\u{(int)c:X4}",
                _ => c.ToString(),
            });
        return sb.Append('"').ToString();
    }

    const string ProgramCs = """
        // Generated entry point. Do not edit.
        public static class Program
        {
            public static void Main(string[] args)
            {
                Rt.Args = args;
                Rt.Init();
                System.Console.OutputEncoding = new System.Text.UTF8Encoding(false);
                // Deep recursion is how loops are written, so run on a big stack.
                int code = 0;
                var t = new System.Threading.Thread(() =>
                {
                    try { Fn.@main(); }
                    catch (System.Exception e)
                    {
                        // Debugging aid: a clean one-line error; AIP_TRACE=1 adds the C# stack (function names are Fn.<name>).
                        var trace = System.Environment.GetEnvironmentVariable("AIP_TRACE");
                        System.Console.Error.WriteLine("runtime error: " + e.GetType().Name + ": " + e.Message);
                        if (trace == "1" || trace == "true") System.Console.Error.WriteLine(e.StackTrace);
                        else System.Console.Error.WriteLine("  (set AIP_TRACE=1 to see which functions were running)");
                        code = 1;
                        Rt.Error = e.GetType().Name + ": " + e.Message;
                    }
                }, 512 * 1024 * 1024);
                t.Start();
                t.Join();
                Rt.Report(code);
                System.Environment.Exit(code);
            }
        }


        """;

    const string Csproj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>
            <InvariantGlobalization>true</InvariantGlobalization>
            <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
            <DebugType>none</DebugType>
            <GenerateDocumentationFile>false</GenerateDocumentationFile>
          </PropertyGroup>
        </Project>

        """;
}
