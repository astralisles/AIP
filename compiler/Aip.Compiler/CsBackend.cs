using System.Diagnostics;
using System.Text;

namespace Aip;

/// <summary>Back end that emits C# and builds it with the .NET SDK.</summary>
sealed class CsBackend : IBackend
{
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
        ["printLine"] = "System.Console.Out.Write({0} + \"\\n\")",
        ["writeFile"] = "System.IO.File.WriteAllText({0}, {1})",
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
        if (Directory.Exists(srcDir)) Directory.Delete(srcDir, true);
        Directory.CreateDirectory(srcDir);
        foreach (var fn in program.ProjectFns)
            File.WriteAllText(Path.Combine(srcDir, fn.Name + ".cs"), EmitFn(fn));
        File.WriteAllText(Path.Combine(srcDir, "Program.cs"), ProgramCs);
        var csproj = Path.Combine(srcDir, program.Name + ".csproj");
        File.WriteAllText(csproj, Csproj);

        var binDir = Path.Combine(outDir, "bin", target);
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
            Console.WriteLine("error backend-failed: the generated C# did not build (this is a compiler bug or a missing .NET component)");
            foreach (var line in (output + errTask.Result).Split('\n').Where(l => l.Contains("error")).Distinct().Take(10))
                Console.WriteLine("  " + line.Trim());
            return null;
        }
        return Path.Combine(binDir, program.Name + (target.StartsWith("windows") ? ".exe" : ""));
    }

    static string CsType(string t) => t switch { "Int" => "long", "Text" => "string", "Bool" => "bool", _ => "void" };

    static string EmitFn(Fn fn)
    {
        var sb = new StringBuilder();
        sb.Append($"// Generated from {fn.Name}.fn. Do not edit.\n");
        sb.Append("public static partial class Fn\n{\n");
        sb.Append($"    /// <summary>{System.Security.SecurityElement.Escape(fn.Doc)}</summary>\n");
        sb.Append($"    public static {CsType(fn.Ret.Name)} @{fn.Name}({string.Join(", ", fn.Params.Select(p => $"{CsType(p.Type.Name)} @{p.Name}"))})\n    {{\n");
        for (int i = 0; i < fn.Body.Count; i++)
        {
            var s = fn.Body[i];
            var e = Emit(s.Value);
            if (s.Let != null) sb.Append($"        var @{s.Let} = {e};\n");
            else if (i == fn.Body.Count - 1 && fn.Ret.Name != "Unit") sb.Append($"        return {e};\n");
            else sb.Append($"        {e};\n");
        }
        sb.Append("    }\n}\n");
        return sb.ToString();
    }

    static string Emit(Expr e) => e switch
    {
        IntLit i => i.Value + "L",
        BoolLit b => b.Value ? "true" : "false",
        NameRef n => "@" + n.Name,
        TextLit t => t.Parts.Count == 1 && t.Parts[0] is string only
            ? Quote(only)
            : "string.Concat(" + string.Join(", ", t.Parts.Select(p => p is string s ? Quote(s) : $"Rt.Text({Emit((Expr)p)})")) + ")",
        Call c => Externs.TryGetValue(c.Name, out var template)
            ? string.Format(template, c.Args.Select(Emit).ToArray<object>())
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
            public static void Main()
            {
                System.Console.OutputEncoding = new System.Text.UTF8Encoding(false);
                Fn.@main();
            }
        }

        public static class Rt
        {
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
