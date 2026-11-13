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
    public static ProgramModel Load(string dir, List<Diagnostic> diags, Dictionary<string, string[]>? overrides = null, bool stdOnly = false, SourceCache? cache = null, Action<string>? phase = null)
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
            // FileInfo comes from the directory listing, so size and write time cost no extra file opens.
            var infos = new DirectoryInfo(root).EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(f => !Path.GetRelativePath(root, f.FullName).Replace('\\', '/').Split('/').Contains("build"))
                .OrderBy(f => f.FullName, StringComparer.Ordinal).ToArray();
            var files = infos.Select(f => f.FullName).ToArray();
            if (!isStd) phase?.Invoke($"list files ({files.Length})");
            // Reading and parsing is ~90% of the time on big projects (mostly opening files), so it runs on
            // all cores; results are then merged in file order, so output stays the same as a serial run.
            var parsed = new object?[files.Length];
            Parallel.For(0, files.Length, i =>
            {
                if (Path.GetExtension(files[i]) != ".fn") return;
                try
                {
                    parsed[i] = Over(files[i]) is { } o ? Parser.ParseFile(files[i], o)
                        : isStd || cache == null ? Parser.ParseFile(files[i])
                        : Parser.ParseText(files[i], cache.Read(infos[i]));
                }
                catch (CompileException e) { parsed[i] = e.Diagnostic; }
            });
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
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
                if (parsed[i] is Diagnostic d) { diags.Add(d); continue; }
                var fn = (Fn)parsed[i]!;
                fn.IsStd = isStd;
                if (isStd && StdLayoutError(root, file, fn) is { } layout)
                    diags.Add(new Diagnostic(file, fn.Line, "std-layout", layout, "see compiler/std/README.md"));
                if (program.Fns.TryGetValue(fn.Name, out var other))
                    diags.Add(new Diagnostic(file, fn.Line, "duplicate-name", $"`{fn.Name}` is already defined in {Path.GetFileName(other.File)}{(other.IsStd ? " (standard library)" : "")}", "names are unique across the project; rename one"));
                else program.Fns[fn.Name] = fn;
            }
        }

        LoadFolder(Path.Combine(AppContext.BaseDirectory, "std"), true);
        if (stdOnly) return program;
        phase?.Invoke("load cache");
        LoadFolder(dir, false);
        phase?.Invoke($"read+parse ({cache?.Hits ?? 0} from cache, {cache?.Misses ?? 0} from disk)");
        cache?.Save();
        phase?.Invoke("save cache");
        foreach (var (name, ctor) in program.Ctors.Where(c => program.Fns.ContainsKey(c.Key)))
            diags.Add(new Diagnostic(ctor.Def.File, ctor.Case.Line, "duplicate-name", $"`{name}` is also a function ({Path.GetFileName(program.Fns[name].File)})", "names are unique across the project; rename one"));
        if (diags.Count == 0) new Checker(program, diags).Check();
        phase?.Invoke("check");
        return program;
    }
}
