using System.Runtime.InteropServices;
using Aip;

const string Usage = """
    usage:
      aip check <projectDir>                          check only
      aip index <projectDir>                          print the index: one line per function
      aip build <projectDir> [--target T] [--out D]   check, then build a program
      aip run <projectDir>                            check, build for this machine, then run it
      aip lint <projectDir>                           check, then warn about code that is probably wrong
      aip test <projectDir>                           check, then run every `example` and its contracts
      aip emit <projectDir> [--out D]                 check, then write the generated C# (no build)
      aip fuzz <projectDir>                           damage every source file many ways; the compiler must never crash
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
if (args.Length < 2 || args[0] is not ("check" or "index" or "build" or "run" or "lint" or "test" or "emit" or "fuzz" or "std" or "find"))
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
var program = Project.Load(dir, diags, stdOnly: command == "std");
Log($"loaded: {program.Fns.Count} function(s), {program.Types.Count} type(s)");
if (command == "std")
{
    if (diags.Count > 0) { foreach (var d in diags) Console.WriteLine(d); return 1; }
    var stdRoot = Path.Combine(AppContext.BaseDirectory, "std");
    foreach (var g in program.Fns.Values.GroupBy(f => Project.StdGroup(stdRoot, f.File)).OrderBy(g => g.Key, StringComparer.Ordinal))
    {
        Console.WriteLine($"{g.Key}/");
        foreach (var f in g.OrderBy(f => f.Name, StringComparer.Ordinal)) Console.WriteLine($"  {f.Signature} | {f.EffectsText}");
    }
    return 0;
}
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
    case "lint":
        {
            var warnings = Lint.Run(program);
            foreach (var w in warnings.OrderBy(w => w.File, StringComparer.Ordinal).ThenBy(w => w.Line)) Console.WriteLine(w);
            Console.WriteLine($"{warnings.Count} warning(s)");
            return warnings.Count > 0 ? 1 : 0;
        }
    case "test":
        {
            var (passed, skipped, failures, none) = Interp.RunExamples(program);
            foreach (var f in failures.OrderBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Line)) Console.WriteLine(f);
            Console.WriteLine($"{passed} example(s) passed, {failures.Count} failed, {skipped} skipped (need effects); {none.Count} pure function(s) without an example");
            return failures.Count > 0 ? 1 : 0;
        }
    case "emit":
        {
            var emitDir = outDir;
            Directory.CreateDirectory(emitDir);
            foreach (var (name, content) in CsBackend.EmitFiles(program)) File.WriteAllText(Path.Combine(emitDir, name), content);
            File.WriteAllText(Path.Combine(emitDir, "index.txt"), Index());
            Console.WriteLine($"ok: wrote the generated files to {Path.GetRelativePath(Environment.CurrentDirectory, emitDir).Replace('\\', '/')}");
            return 0;
        }
    case "fuzz":
        {
            var (tried, crashes) = Fuzz.Run(dir);
            foreach (var c in crashes.Take(20)) Console.WriteLine("crash: " + c);
            Console.WriteLine($"{tried} damaged input(s) tried, {crashes.Count} crash(es)");
            return crashes.Count > 0 ? 1 : 0;
        }
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
var backend = new CsBackend { Log = Log };
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
