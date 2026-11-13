namespace Aip;

/// <summary>Loads a project folder (plus the standard library) into a <see cref="ProgramModel"/>, collecting parse and name errors.</summary>
static class Project
{
    // std is grouped one folder deep: std/<group>/<name>.fn. The name starts with the group (textSlice in text/),
    // or, for effects, the group is the effect's prefix (printLine in terminal/ has effect terminal.write).
    // Only core/ holds functions that fit neither.
    public static string StdGroup(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/').Split('/') is { Length: 2 } parts ? parts[0] : "";

    static string? StdLayoutError(string root, string file, Fn fn)
    {
        var group = StdGroup(root, file);
        if (group == "") return $"std function `{fn.Name}` must be in a group folder, std/<group>/{fn.Name}.fn";
        if (group == "core" || fn.Name.StartsWith(group, StringComparison.Ordinal)
            || fn.Effects.Any(e => e.StartsWith(group + ".", StringComparison.Ordinal))) return null;
        return $"std function `{fn.Name}` in std/{group}/ must start with `{group}` or have an effect `{group}.*`";
    }

    /// <param name="overrides">Optional replacement text per file path, so tools can try a changed file without writing it.</param>
    public static ProgramModel Load(string dir, List<Diagnostic> diags, Dictionary<string, string[]>? overrides = null, bool stdOnly = false)
    {
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

        string[]? Over(string file) => overrides != null && overrides.TryGetValue(file, out var l) ? l : null;

        void LoadFolder(string root, bool isStd)
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
                        try { AddType(Parser.ParseTypeFile(file, Over(file))); }
                        catch (CompileException e) { diags.Add(e.Diagnostic); }
                    }
                    else if (ext == ".const")
                        diags.Add(new Diagnostic(file, 1, "unsupported-file", $"`{ext}` files are not supported by the compiler yet"));
                    continue;
                }
                try
                {
                    var fn = Parser.ParseFile(file, Over(file));
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

        LoadFolder(Path.Combine(AppContext.BaseDirectory, "std"), true);
        if (stdOnly) return program;
        LoadFolder(dir, false);
        foreach (var (name, ctor) in program.Ctors.Where(c => program.Fns.ContainsKey(c.Key)))
            diags.Add(new Diagnostic(ctor.Def.File, ctor.Case.Line, "duplicate-name", $"`{name}` is also a function ({Path.GetFileName(program.Fns[name].File)})", "names are unique across the project; rename one"));
        if (diags.Count == 0) new Checker(program, diags).Check();
        return program;
    }
}
