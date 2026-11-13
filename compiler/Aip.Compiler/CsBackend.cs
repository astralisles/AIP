using System.Diagnostics;
using System.Text;

namespace Aip;

/// <summary>Back end that emits C# and builds it with the .NET SDK.</summary>
sealed class CsBackend : IBackend
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

    /// <summary>How each std extern function is written in C#. {0}, {1}... are the arguments.</summary>
    static readonly Dictionary<string, string> Externs = new()
    {
        ["logLine"] = "System.Console.Error.Write({0} + \"\\n\")",
        ["printLine"] = "System.Console.Out.Write({0} + \"\\n\")",
        ["textLength"] = "(long)({0}).Length",
        ["textSlice"] = "Rt.Slice({0}, {1}, {2})",
        ["textCode"] = "(long)({0})[(int)({1})]",
        ["textFromCode"] = "((char)({0})).ToString()",
        ["textToInt"] = "long.Parse({0}, System.Globalization.CultureInfo.InvariantCulture)",
        ["listEmpty"] = "new System.Collections.Generic.List<object>()",
        ["listLength"] = "(long)({0}).Count",
        ["listGet"] = "({0})[(int)({1})]",
        ["listAppend"] = "Rt.Append({0}, {1})",
        ["mapEmpty"] = "new System.Collections.Generic.Dictionary<object, object>()",
        ["mapSize"] = "(long)({0}).Count",
        ["mapHas"] = "({0}).ContainsKey({1})",
        ["mapGet"] = "Rt.MapGet({0}, {1})",
        ["mapSet"] = "Rt.MapSet({0}, {1}, {2})",
        ["mapRemove"] = "Rt.MapRemove({0}, {1})",
        ["mapKeys"] = "new System.Collections.Generic.List<object>(({0}).Keys)",
        ["setEmpty"] = "new System.Collections.Generic.Dictionary<object, object>()",
        ["setSize"] = "(long)({0}).Count",
        ["setHas"] = "({0}).ContainsKey({1})",
        ["setAdd"] = "Rt.MapSet({0}, {1}, true)",
        ["setRemove"] = "Rt.MapRemove({0}, {1})",
        ["setItems"] = "new System.Collections.Generic.List<object>(({0}).Keys)",
        ["sortedMapEmpty"] = "new System.Collections.Generic.SortedDictionary<object, object>(Rt.Order)",
        ["sortedMapSize"] = "(long)({0}).Count",
        ["sortedMapHas"] = "({0}).ContainsKey({1})",
        ["sortedMapGet"] = "Rt.SortedGet({0}, {1})",
        ["sortedMapSet"] = "Rt.SortedPut({0}, {1}, {2})",
        ["sortedMapRemove"] = "Rt.SortedRemove({0}, {1})",
        ["sortedMapKeys"] = "new System.Collections.Generic.List<object>(({0}).Keys)",
        ["sortedSetEmpty"] = "new System.Collections.Generic.SortedDictionary<object, object>(Rt.Order)",
        ["sortedSetSize"] = "(long)({0}).Count",
        ["sortedSetHas"] = "({0}).ContainsKey({1})",
        ["sortedSetAdd"] = "Rt.SortedPut({0}, {1}, true)",
        ["sortedSetRemove"] = "Rt.SortedRemove({0}, {1})",
        ["sortedSetItems"] = "new System.Collections.Generic.List<object>(({0}).Keys)",
        ["fileRead"] = "System.IO.File.ReadAllText({0})",
        ["fileWrite"] = "Rt.Write({0}, {1})",
        ["noop"] = "Rt.Nop()",
        ["argCount"] = "(long)Rt.Args.Length",
        ["arg"] = "Rt.Args[(int)({0})]",
    };

    public string? Build(ProgramModel program, string outDir, string target)
    {
        if (!Targets.TryGetValue(target, out var rid))
        {
            Console.WriteLine($"error unsupported-target: the cs back end cannot build for `{target}` yet\n  fix: use one of: {string.Join(", ", Targets.Keys)}");
            return null;
        }
        foreach (var fn in program.Fns.Values.Where(f => f.IsExtern && !Externs.ContainsKey(f.Name)))
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

    /// <summary>All generated files (name to content), in a fixed order. Pure: used by the build and by `aip emit`.</summary>
    public static SortedDictionary<string, string> EmitFiles(ProgramModel program)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var fn in program.ProjectFns) files[fn.Name + ".cs"] = EmitFn(fn);
        files["Program.cs"] = ProgramCs;
        if (program.Types.Count > 0) files["Types.cs"] = EmitTypes(program);
        files[program.Name + ".csproj"] = Csproj;
        return files;
    }

    /// <summary>The names of the standard-library functions the back end implements.</summary>
    public static IEnumerable<string> ExternNames => Externs.Keys.OrderBy(k => k, StringComparer.Ordinal);

    // Lists are type-erased at run time (List<object>); reads cast back to the element type.
    static string CsType(string t) => t switch
    {
        "Int" => "long",
        "Text" => "string",
        "Bool" => "bool",
        "_" => "object",
        _ when t.StartsWith("List<", StringComparison.Ordinal) => "System.Collections.Generic.List<object>",
        // Maps and sets are insertion-ordered dictionaries (a set maps each item to true); every change copies.
        _ when t.StartsWith("Map<", StringComparison.Ordinal) || t.StartsWith("Set<", StringComparison.Ordinal) => "System.Collections.Generic.Dictionary<object, object>",
        _ when t.StartsWith("SortedMap<", StringComparison.Ordinal) || t.StartsWith("SortedSet<", StringComparison.Ordinal) => "System.Collections.Generic.SortedDictionary<object, object>",
        "Unit" => "void",
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
            if (!def.IsVariant) sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"public sealed record @{def.Name}({Fields(def.Cases[0])});\n");
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
        _ => $"{indent}{Emit(e)};\n",
    };

    static string Emit(Expr e) => e switch
    {
        IntLit i => i.Value + "L",
        BoolLit b => b.Value ? "true" : "false",
        NameRef n => "@" + n.Name,
        TextLit t => t.Parts.Count == 1 && t.Parts[0] is string only
            ? Quote(only)
            : "string.Concat(" + string.Join(", ", t.Parts.Select(p => p is string s ? Quote(s) : $"Rt.Text({Emit((Expr)p)})")) + ")",
        Call { Ctor: not null } c => $"new {c.Ctor}({string.Join(", ", c.Args.Select(Emit))})",
        Field f => $"{Emit(f.Target)}.@{f.Name}",
        MatchExpr m => $"({Emit(m.Subject)} switch {{ {string.Join(", ", m.Arms.Select(a => $"{Pattern(a)} => {Emit(a.Body)}"))}, _ => throw new System.InvalidOperationException() }})",
        Call c => Externs.TryGetValue(c.Name, out var template)
            ? (c.Name is "listGet" or "mapGet" or "sortedMapGet" ? $"(({CsType(c.Type)})" : "") + string.Format(System.Globalization.CultureInfo.InvariantCulture, template, c.Args.Select(Emit).ToArray<object>()) + (c.Name is "listGet" or "mapGet" or "sortedMapGet" ? ")" : "")
            : $"Fn.@{c.Name}({string.Join(", ", c.Args.Select(Emit))})",
        Unary u => u.Op == "-" ? $"(-{Emit(u.Operand)})" : $"(!{Emit(u.Operand)})",
        Binary b => b.Op switch
        {
            "and" => $"({Emit(b.Left)} && {Emit(b.Right)})",
            "or" => $"({Emit(b.Left)} || {Emit(b.Right)})",
            "=>" => $"(!{Emit(b.Left)} || {Emit(b.Right)})",
            _ => $"({Emit(b.Left)} {b.Op} {Emit(b.Right)})",
        },
        IfExpr i => $"({Emit(i.Cond)} ? {Emit(i.Then)} : {Emit(i.Else)})",
        _ => throw new InvalidOperationException("unknown expression"),
    };

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

        public static class Rt
        {
            public static string[] Args = System.Array.Empty<string>();
            public static string Slice(string s, long start, long end)
            {
                int a = (int)System.Math.Clamp(start, 0, s.Length), b = (int)System.Math.Clamp(end, a, s.Length);
                return s.Substring(a, b - a);
            }
            public static System.Collections.Generic.List<object> Append(System.Collections.Generic.List<object> l, object item)
            {
                var r = new System.Collections.Generic.List<object>(l.Count + 1);
                r.AddRange(l);
                r.Add(item);
                return r;
            }
            public static object MapGet(System.Collections.Generic.Dictionary<object, object> m, object k) =>
                m.TryGetValue(k, out var v) ? v : throw new System.Collections.Generic.KeyNotFoundException("mapGet: key not in the map");
            public static System.Collections.Generic.Dictionary<object, object> MapSet(System.Collections.Generic.Dictionary<object, object> m, object k, object v)
            {
                var r = new System.Collections.Generic.Dictionary<object, object>(m);
                r[k] = v;
                return r;
            }
            public static System.Collections.Generic.Dictionary<object, object> MapRemove(System.Collections.Generic.Dictionary<object, object> m, object k)
            {
                var r = new System.Collections.Generic.Dictionary<object, object>();
                foreach (var p in m) if (!p.Key.Equals(k)) r.Add(p.Key, p.Value);
                return r;
            }
            // Sorted maps and sets are balanced trees: keys come back in ascending order (Int by value, Text by ordinal).
            sealed class OrderCmp : System.Collections.Generic.IComparer<object>
            {
                public int Compare(object? a, object? b) => a is string s ? string.CompareOrdinal(s, (string)b!) : ((long)a!).CompareTo((long)b!);
            }
            public static readonly System.Collections.Generic.IComparer<object> Order = new OrderCmp();
            public static object SortedGet(System.Collections.Generic.SortedDictionary<object, object> m, object k) =>
                m.TryGetValue(k, out var v) ? v : throw new System.Collections.Generic.KeyNotFoundException("sortedMapGet: key not in the map");
            public static System.Collections.Generic.SortedDictionary<object, object> SortedPut(System.Collections.Generic.SortedDictionary<object, object> m, object k, object v)
            {
                var r = new System.Collections.Generic.SortedDictionary<object, object>(m, Order);
                r[k] = v;
                return r;
            }
            public static System.Collections.Generic.SortedDictionary<object, object> SortedRemove(System.Collections.Generic.SortedDictionary<object, object> m, object k)
            {
                var r = new System.Collections.Generic.SortedDictionary<object, object>(m, Order);
                r.Remove(k);
                return r;
            }
            public static void Nop() { }

            // ---- opt-in profiling and call log (one program thread; parallel tasks (T-0935) will need per-thread state) ----
            public static bool Prof, Calls;
            public static string? Error;
            static string? profPath, callsPath;
            static long startTs, depth, maxDepth, callLimit, callCount;
            static System.Text.StringBuilder callLog = new();
            sealed class Stat { public long Calls, Self; }
            static readonly System.Collections.Generic.Dictionary<string, Stat> stats = new();
            static readonly System.Collections.Generic.List<long> childTime = new();
            static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
            public static void Init()
            {
                profPath = System.Environment.GetEnvironmentVariable("AIP_PROFILE");
                callsPath = System.Environment.GetEnvironmentVariable("AIP_CALLS");
                Prof = !string.IsNullOrEmpty(profPath);
                Calls = !string.IsNullOrEmpty(callsPath);
                callLimit = long.TryParse(System.Environment.GetEnvironmentVariable("AIP_CALLS_MAX"), out var n) ? n : 10000;
                startTs = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            public static long Enter()
            {
                depth++;
                if (depth > maxDepth) maxDepth = depth;
                childTime.Add(0);
                return System.Diagnostics.Stopwatch.GetTimestamp();
            }
            public static void Leave(string name, long t0)
            {
                long total = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                long kids = childTime[^1];
                childTime.RemoveAt(childTime.Count - 1);
                if (childTime.Count > 0) childTime[^1] += total;
                if (!stats.TryGetValue(name, out var s)) stats[name] = s = new Stat();
                s.Calls++;
                s.Self += total - kids;
                depth--;
            }
            public static void Call(string name, object[] a)
            {
                callCount++;
                if (callCount > callLimit) return;
                callLog.Append("{\"n\":").Append(callCount).Append(",\"depth\":").Append(depth).Append(",\"fn\":\"").Append(name).Append("\",\"args\":[");
                for (int i = 0; i < a.Length; i++)
                {
                    var v = a[i] is string str ? str : System.Convert.ToString(a[i], Inv) ?? "null";
                    if (v.Length > 120) v = v.Substring(0, 120) + "...";
                    callLog.Append(i > 0 ? "," : "").Append(Json(v));
                }
                callLog.Append("]}\n");
            }
            static string Json(string v)
            {
                var sb = new System.Text.StringBuilder("\"");
                foreach (var c in v)
                    sb.Append(c switch { '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", _ when c < ' ' => "\\u" + ((int)c).ToString("X4"), _ => c.ToString() });
                return sb.Append('"').ToString();
            }
            static void Out(string? path, string text)
            {
                if (path == "-") System.Console.Error.Write(text);
                else if (path != null) System.IO.File.WriteAllText(path, text);
            }
            public static void Report(int code)
            {
                if (Calls)
                {
                    if (callCount > callLimit) callLog.Append("{\"truncated\":true,\"calls\":").Append(callCount).Append(",\"logged\":").Append(callLimit).Append("}\n");
                    Out(callsPath, callLog.ToString());
                }
                if (!Prof) return;
                string ms(long ts) => (ts * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.###", Inv);
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                var sb = new System.Text.StringBuilder("{\n");
                sb.Append($"  \"schema\": 1,\n  \"exitCode\": {code},\n  \"error\": {(Error == null ? "null" : Json(Error))},\n");
                sb.Append($"  \"wallMs\": {ms(System.Diagnostics.Stopwatch.GetTimestamp() - startTs)},\n");
                sb.Append($"  \"cpuMs\": {proc.TotalProcessorTime.TotalMilliseconds.ToString("0.###", Inv)},\n");
                sb.Append($"  \"peakWorkingSetBytes\": {proc.PeakWorkingSet64},\n  \"allocatedBytes\": {System.GC.GetTotalAllocatedBytes()},\n");
                sb.Append($"  \"gcCollections\": [{System.GC.CollectionCount(0)}, {System.GC.CollectionCount(1)}, {System.GC.CollectionCount(2)}],\n");
                sb.Append($"  \"maxCallDepth\": {maxDepth},\n  \"functions\": [\n");
                sb.Append(string.Join(",\n", System.Linq.Enumerable.Select(System.Linq.Enumerable.OrderByDescending(stats, p => p.Value.Self),
                    p => $"    {{\"fn\": \"{p.Key}\", \"calls\": {p.Value.Calls}, \"selfMs\": {ms(p.Value.Self)}}}")));
                sb.Append("\n  ]\n}\n");
                Out(profPath, sb.ToString());
            }
            public static void Write(string path, string content) => System.IO.File.WriteAllText(path, content);
            public static string Text(string v) => v;
            public static string Text(long v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            public static string Text(bool v) => v ? "true" : "false";
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
