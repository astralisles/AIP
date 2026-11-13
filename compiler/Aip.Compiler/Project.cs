namespace Aip;

/// <summary>Loads a project folder (plus the standard library) into a <see cref="ProgramModel"/>, collecting parse and name errors.</summary>
static class Project
{
    /// <param name="overrides">Optional replacement text per file path, so tools can try a changed file without writing it.</param>
    public static ProgramModel Load(string dir, List<Diagnostic> diags, Dictionary<string, string[]>? overrides = null)
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
                    if (program.Fns.TryGetValue(fn.Name, out var other))
                        diags.Add(new Diagnostic(file, fn.Line, "duplicate-name", $"`{fn.Name}` is already defined in {Path.GetFileName(other.File)}{(other.IsStd ? " (standard library)" : "")}", "names are unique across the project; rename one"));
                    else program.Fns[fn.Name] = fn;
                }
                catch (CompileException e) { diags.Add(e.Diagnostic); }
            }
        }

        LoadFolder(Path.Combine(AppContext.BaseDirectory, "std"), true);
        LoadFolder(dir, false);
        foreach (var (name, ctor) in program.Ctors.Where(c => program.Fns.ContainsKey(c.Key)))
            diags.Add(new Diagnostic(ctor.Def.File, ctor.Case.Line, "duplicate-name", $"`{name}` is also a function ({Path.GetFileName(program.Fns[name].File)})", "names are unique across the project; rename one"));
        if (diags.Count == 0) new Checker(program, diags).Check();
        return program;
    }
}
