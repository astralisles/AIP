namespace Aip;

/// <summary>A package shipped beside the compiler (packages/&lt;name&gt;/): optional functions with their own NuGet dependency (T-0987).</summary>
sealed class PackageInfo
{
    public string Name = "";
    public string Version = "";
    public string Dir = "";
    public List<(string Id, string Version)> Nuget = new();
    public List<string> Handles = new();
    /// <summary>Function name to C# template ({0}, {1}... are the arguments), like CsBackend `Externs` for std.</summary>
    public Dictionary<string, string> Externs = new();
}

static class Packages
{
    public static string Root => Path.Combine(AppContext.BaseDirectory, "packages");

    /// <summary>Reads packages/&lt;name&gt;/package.txt; null with a message when it is missing or broken.</summary>
    public static PackageInfo? Read(string name, out string error)
    {
        error = "";
        var dir = Path.Combine(Root, name);
        var file = Path.Combine(dir, "package.txt");
        if (!Directory.Exists(dir) || !File.Exists(file))
        {
            var known = Directory.Exists(Root) ? string.Join(", ", Directory.EnumerateDirectories(Root).Select(Path.GetFileName).Order(StringComparer.Ordinal)) : "";
            error = $"no package `{name}`; available: {(known == "" ? "none" : known)}";
            return null;
        }
        var p = new PackageInfo { Dir = dir };
        int n = 0;
        foreach (var raw in File.ReadAllLines(file))
        {
            n++;
            var line = raw.Trim();
            if (line == "" || line[0] == '#') continue;
            var parts = line.Split(' ', 2, StringSplitOptions.TrimEntries);
            var arg = parts.Length > 1 ? parts[1] : "";
            switch (parts[0])
            {
                case "name": p.Name = arg; break;
                case "version": p.Version = arg; break;
                case "handle": p.Handles.Add(arg); break;
                case "nuget" when arg.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var id, var v]: p.Nuget.Add((id, v)); break;
                case "extern" when arg.Split(' ', 2) is [var fn, var template]: p.Externs[fn] = template.Trim(); break;
                default: error = $"package.txt line {n} of `{name}` is not understood: {line}"; return null;
            }
        }
        if (p.Name != name || p.Version == "") { error = $"package.txt of `{name}` must say `name {name}` and a `version`"; return null; }
        return p;
    }

    /// <summary>Reads the project's packages.txt (one `name version` per line, # comments). The version must match the shipped package exactly.</summary>
    public static List<PackageInfo> Declared(string projectDir, List<Diagnostic> diags)
    {
        var result = new List<PackageInfo>();
        var file = Path.Combine(projectDir, "packages.txt");
        if (!File.Exists(file)) return result;
        var lines = File.ReadAllLines(file);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line == "" || line[0] == '#') continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) { diags.Add(new Diagnostic(file, i + 1, "bad-packages-line", $"`{line}` is not `<package> <version>`", "write one package per line, for example `mongo 1.0.0`")); continue; }
            if (result.Any(r => r.Name == parts[0])) { diags.Add(new Diagnostic(file, i + 1, "bad-packages-line", $"package `{parts[0]}` is listed twice", "list each package once")); continue; }
            var info = Read(parts[0], out var error);
            if (info == null) { diags.Add(new Diagnostic(file, i + 1, "unknown-package", error, "fix the name, or remove the line")); continue; }
            if (info.Version != parts[1]) { diags.Add(new Diagnostic(file, i + 1, "package-version", $"package `{info.Name}` is version {info.Version} here, not {parts[1]}", $"write `{info.Name} {info.Version}`")); continue; }
            result.Add(info);
        }
        return result;
    }
}
