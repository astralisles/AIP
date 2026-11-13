using System.Runtime.InteropServices;
using Aip;

const string Usage = """
    usage:
      aip check <projectDir>                          check only
      aip index <projectDir>                          print the index: one line per function
      aip build <projectDir> [--target T] [--out D]   check, then build a program
      aip run <projectDir>                            check, build for this machine, then run it
      aip std                                         list the standard library by group (std/<group>/)
      aip find <projectDir> <word>...                 search project and std functions by name, doc, effect (best match first)
      aip spec                                        print the one-page language spec (for a model to read first)
    options: --verbose (or -v, or env AIP_VERBOSE=1) logs compiler steps and timings to stderr
    run-time: AIP_TRACE=1 makes a crashing program print its full stack trace; `logLine` writes to stderr
    targets: windows-x64, windows-arm64, linux-x64, linux-arm64, macos-arm64 (default: this machine)
    """;

if (args.Length == 2 && args[0] == "tokens")
{
    Console.Write(Parser.Tokens(Path.GetFullPath(args[1])));
    return 0;
}
if (args is ["spec"])
{
    Console.Write(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "spec.md")));
    return 0;
}
string[] findWords = [];
if (args.Length >= 3 && args[0] == "find") { findWords = args[2..]; args = args[..2]; }
if (args is ["std"]) args = ["std", AppContext.BaseDirectory];
if (args.Length < 2 || args[0] is not ("check" or "index" or "build" or "run" or "std" or "find"))
{
    Console.WriteLine(Usage);
    return 2;
}

var command = args[0];
var dir = Path.GetFullPath(args[1]);
string? target = null, outDir = null;
bool verbose = Environment.GetEnvironmentVariable("AIP_VERBOSE") is "1" or "true";
for (int i = 2; i < args.Length; i++)
{
    if (args[i] is "--verbose" or "-v") verbose = true;
    else if (args[i] == "--target" && i + 1 < args.Length) target = args[++i];
    else if (args[i] == "--out" && i + 1 < args.Length) outDir = Path.GetFullPath(args[++i]);
    else { Console.WriteLine($"unknown option `{args[i]}`\n{Usage}"); return 2; }
}
if (!Directory.Exists(dir)) { Console.WriteLine($"error no-project: `{args[1]}` is not a folder"); return 1; }
outDir ??= Path.Combine(dir, "build");
var clock = System.Diagnostics.Stopwatch.StartNew();
void Log(string msg) { if (verbose) Console.Error.WriteLine($"[aip {clock.ElapsedMilliseconds,5} ms] {msg}"); }
Log($"{command} {dir}");

var diags = new List<Diagnostic>();
var program = new ProgramModel { Name = Path.GetFileName(dir.TrimEnd('\\', '/')), Dir = dir };

void AddType(TypeDef def)
{
    if (program.Types.TryGetValue(def.Name, out var other))
    {
        diags.Add(new Diagnostic(def.File, def.Line, "duplicate-name", $"`{def.Name}` is already defined in {Path.GetFileName(other.File)}", "names are unique across the project; rename one"));
        return;
    }
    program.Types[def.Name] = def;
    foreach (var c in def.Cases)
        if (!program.Ctors.TryAdd(c.Name, (def, c)))
            diags.Add(new Diagnostic(def.File, c.Line, "duplicate-name", $"`{c.Name}` is already defined in {Path.GetFileName(program.Ctors[c.Name].Def.File)}", "case and type names are unique across the project; rename one"));
}

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
            if (ext is ".struct" or ".variant")
            {
                try { AddType(Parser.ParseTypeFile(file)); }
                catch (CompileException e) { diags.Add(e.Diagnostic); }
            }
            else if (ext == ".const")
                diags.Add(new Diagnostic(file, 1, "unsupported-file", $"`{ext}` files are not supported by the compiler yet"));
            continue;
        }
        try
        {
            var fn = Parser.ParseFile(file);
            fn.IsStd = isStd;
            if (isStd && StdLayoutError(root, file, fn) is { } layout)
                diags.Add(new Diagnostic(file, fn.Line, "std-layout", layout, "see compiler/std/README.md"));
            if (program.Fns.TryGetValue(fn.Name, out var other))
                diags.Add(new Diagnostic(file, fn.Line, "duplicate-name", $"`{fn.Name}` is already defined in {Path.GetFileName(other.File)}{(other.IsStd ? " (standard library)" : "")}", "names are unique across the project; rename one"));
            else program.Fns[fn.Name] = fn;
        }
        catch (CompileException e) { diags.Add(e.Diagnostic); }
    }
}

// std is grouped one folder deep: std/<group>/<name>.fn. The name starts with the group (textSlice in text/),
// or, for effects, the group is the effect's prefix (printLine in terminal/ has effect terminal.write).
// Only core/ holds functions that fit neither.
static string StdGroup(string root, string file) =>
    Path.GetRelativePath(root, file).Replace('\\', '/').Split('/') is { Length: 2 } parts ? parts[0] : "";

static string? StdLayoutError(string root, string file, Fn fn)
{
    var group = StdGroup(root, file);
    if (group == "") return $"std function `{fn.Name}` must be in a group folder, std/<group>/{fn.Name}.fn";
    if (group == "core" || fn.Name.StartsWith(group, StringComparison.Ordinal)
        || fn.Effects.Any(e => e.StartsWith(group + ".", StringComparison.Ordinal))) return null;
    return $"std function `{fn.Name}` in std/{group}/ must start with `{group}` or have an effect `{group}.*`";
}

var stdRoot = Path.Combine(AppContext.BaseDirectory, "std");
Load(stdRoot, true);
Log($"loaded standard library: {program.Fns.Count} function(s)");
if (command == "std")
{
    if (diags.Count > 0) { foreach (var d in diags) Console.WriteLine(d); return 1; }
    foreach (var g in program.Fns.Values.GroupBy(f => StdGroup(stdRoot, f.File)).OrderBy(g => g.Key, StringComparer.Ordinal))
    {
        Console.WriteLine($"{g.Key}/");
        foreach (var f in g.OrderBy(f => f.Name, StringComparer.Ordinal)) Console.WriteLine($"  {f.Signature} | {f.EffectsText}");
    }
    return 0;
}
Load(dir, false);
Log($"loaded project: {program.ProjectFns.Count()} function(s), {program.Types.Count} type(s)");
foreach (var (name, ctor) in program.Ctors.Where(c => program.Fns.ContainsKey(c.Key)))
    diags.Add(new Diagnostic(ctor.Def.File, ctor.Case.Line, "duplicate-name", $"`{name}` is also a function ({Path.GetFileName(program.Fns[name].File)})", "names are unique across the project; rename one"));
if (command == "find")
{
    if (findWords.Length == 0) { Console.WriteLine(Usage); return 2; }
    var hits = program.Fns.Values.Select(f =>
    {
        int score = 0;
        foreach (var w in findWords)
        {
            if (f.Name.Contains(w, StringComparison.OrdinalIgnoreCase)) score += 3;
            if (f.Doc.Contains(w, StringComparison.OrdinalIgnoreCase)) score += 2;
            if (f.Signature.Contains(w, StringComparison.OrdinalIgnoreCase) || f.EffectsText.Contains(w, StringComparison.OrdinalIgnoreCase)) score += 1;
        }
        return (f, score);
    }).Where(h => h.score > 0).OrderByDescending(h => h.score).ThenBy(h => h.f.Name, StringComparer.Ordinal).Take(10).ToList();
    foreach (var (f, _) in hits) Console.WriteLine($"{(f.IsStd ? "std" : "project")} | {f.Signature} | {f.EffectsText} | {f.Doc}");
    if (hits.Count == 0) Console.WriteLine($"no function matches: {string.Join(" ", findWords)}");
    return 0;
}
if (diags.Count == 0) new Checker(program, diags).Check();

Log($"checked: {diags.Count} error(s)");
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
IBackend backend = new CsBackend { Log = Log };
Log($"building for {target} into {outDir}");
var exe = backend.Build(program, outDir, target);
if (exe == null) { Log("build failed"); return 1; }
Log($"built {exe}");
if (command == "run")
{
    Log($"running {exe}");
    using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false })!;
    proc.WaitForExit();
    Log($"program exited with code {proc.ExitCode}");
    return proc.ExitCode;
}
Console.WriteLine($"ok: built {Path.GetRelativePath(Environment.CurrentDirectory, exe).Replace('\\', '/')} ({target}, {backend.Name} back end)");
return 0;
