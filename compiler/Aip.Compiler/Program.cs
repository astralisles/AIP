using System.Runtime.InteropServices;
using Aip;

const string Usage = """
    usage:
      aip check <projectDir>                          check only
      aip index <projectDir>                          print the index: one line per function
      aip build <projectDir> [--target T] [--out D]   check, then build a program
      aip run <projectDir>                            check, build for this machine, then run it
    targets: windows-x64, windows-arm64, linux-x64, linux-arm64, macos-arm64 (default: this machine)
    """;

if (args.Length == 2 && args[0] == "tokens")
{
    Console.Write(Parser.Tokens(Path.GetFullPath(args[1])));
    return 0;
}
if (args.Length < 2 || args[0] is not ("check" or "index" or "build" or "run"))
{
    Console.WriteLine(Usage);
    return 2;
}

var command = args[0];
var dir = Path.GetFullPath(args[1]);
string? target = null, outDir = null;
for (int i = 2; i < args.Length; i++)
{
    if (args[i] == "--target" && i + 1 < args.Length) target = args[++i];
    else if (args[i] == "--out" && i + 1 < args.Length) outDir = Path.GetFullPath(args[++i]);
    else { Console.WriteLine($"unknown option `{args[i]}`\n{Usage}"); return 2; }
}
if (!Directory.Exists(dir)) { Console.WriteLine($"error no-project: `{args[1]}` is not a folder"); return 1; }
outDir ??= Path.Combine(dir, "build");

var diags = new List<Diagnostic>();
var program = new ProgramModel { Name = Path.GetFileName(dir.TrimEnd('\\', '/')), Dir = dir };

void Load(string root, bool isStd)
{
    var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Where(f => !Path.GetRelativePath(root, f).Replace('\\', '/').Split('/').Contains("build"))
        .OrderBy(f => f, StringComparer.Ordinal);
    foreach (var file in files)
    {
        var ext = Path.GetExtension(file);
        if (ext != ".fn")
        {
            if (ext is ".struct" or ".variant" or ".const")
                diags.Add(new Diagnostic(file, 1, "unsupported-file", $"`{ext}` files are not supported by the compiler yet"));
            continue;
        }
        try
        {
            var fn = Parser.ParseFile(file);
            fn.IsStd = isStd;
            if (program.Fns.TryGetValue(fn.Name, out var other))
                diags.Add(new Diagnostic(file, fn.Line, "duplicate-name", $"`{fn.Name}` is already defined in {Path.GetFileName(other.File)}{(other.IsStd ? " (standard library)" : "")}", "names are unique across the project; rename one"));
            else program.Fns[fn.Name] = fn;
        }
        catch (CompileException e) { diags.Add(e.Diagnostic); }
    }
}

Load(Path.Combine(AppContext.BaseDirectory, "std"), true);
Load(dir, false);
if (diags.Count == 0) new Checker(program, diags).Check();

if (diags.Count > 0)
{
    foreach (var d in diags.OrderBy(d => d.File, StringComparer.Ordinal).ThenBy(d => d.Line)) Console.WriteLine(d);
    Console.WriteLine($"{diags.Count} error(s)");
    return 1;
}

string Index() => string.Join("\n", program.ProjectFns.Select(f => $"{f.Signature} | {f.EffectsText} | checked, not proved")) + "\n";

switch (command)
{
    case "check":
        Console.WriteLine($"ok: {program.ProjectFns.Count()} function(s) checked");
        return 0;
    case "index":
        Console.Write(Index());
        return 0;
}

var host = (OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux")
    + (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "-arm64" : "-x64");
if (command == "run" && target != null && target != host)
{
    Console.WriteLine($"error cannot-run-target: `{target}` programs cannot run on this machine ({host})\n  fix: use `aip build` for other targets, or drop --target");
    return 1;
}
target ??= host;
Directory.CreateDirectory(outDir);
File.WriteAllText(Path.Combine(outDir, "index.txt"), Index());
IBackend backend = new CsBackend();
var exe = backend.Build(program, outDir, target);
if (exe == null) return 1;
if (command == "run")
{
    using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false })!;
    proc.WaitForExit();
    return proc.ExitCode;
}
Console.WriteLine($"ok: built {Path.GetRelativePath(Environment.CurrentDirectory, exe).Replace('\\', '/')} ({target}, {backend.Name} back end)");
return 0;
