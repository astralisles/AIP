namespace Aip;

/// <summary>
/// Robustness check for the front end: damages each source file in many small ways and runs the whole front end
/// (parse, check, lint, emit C#) on the result. Any outcome is fine except an unhandled exception: bad input must give
/// a diagnostic, never a crash. Deterministic (no random numbers).
/// </summary>
static class Fuzz
{
    static IEnumerable<(string Name, string[] Lines)> Mutants(string[] lines)
    {
        yield return ("empty file", Array.Empty<string>());
        for (int n = 1; n < lines.Length; n++) yield return ($"first {n} line(s) only", lines[..n]);
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            yield return ($"delete line {i + 1}", lines.Where((_, k) => k != i).ToArray());
            yield return ($"duplicate line {i + 1}", lines.Take(i + 1).Append(l).Concat(lines.Skip(i + 1)).ToArray());
            if (i + 1 < lines.Length)
                yield return ($"swap lines {i + 1} and {i + 2}", lines.Select((x, k) => k == i ? lines[i + 1] : k == i + 1 ? lines[i] : x).ToArray());
            yield return ($"blank line {i + 1}", lines.Select((x, k) => k == i ? "" : x).ToArray());
            if (l.Length > 2)
            {
                foreach (var cut in new[] { 1, l.Length / 3, l.Length / 2, l.Length - 1 }.Distinct())
                    yield return ($"cut line {i + 1} at {cut}", lines.Select((x, k) => k == i ? l[..cut] : x).ToArray());
                yield return ($"drop first character of line {i + 1}", lines.Select((x, k) => k == i ? l[1..] : x).ToArray());
                yield return ($"drop indent of line {i + 1}", lines.Select((x, k) => k == i ? l.TrimStart() : x).ToArray());
                yield return ($"indent line {i + 1} by one", lines.Select((x, k) => k == i ? " " + l : x).ToArray());
                foreach (var junk in new[] { "\"", "(", ")", "{", "}", "<", ",", "..", "=>", "9999999999999999999999" })
                    yield return ($"insert `{junk}` into line {i + 1}", lines.Select((x, k) => k == i ? l.Insert(l.Length / 2, junk) : x).ToArray());
            }
        }
    }

    /// <summary>Returns the number of mutants tried and the descriptions of those that crashed the front end.</summary>
    public static (int Tried, List<string> Crashes) Run(string dir)
    {
        int tried = 0;
        var crashes = new List<string>();
        var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f) is ".fn" or ".struct" or ".variant")
            .Where(f => !Path.GetRelativePath(dir, f).Replace('\\', '/').Split('/').Contains("build"))
            .OrderBy(f => f, StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (var (name, lines) in Mutants(File.ReadAllLines(file)))
            {
                tried++;
                try
                {
                    var diags = new List<Diagnostic>();
                    var program = Project.Load(dir, diags, new Dictionary<string, string[]> { [file] = lines });
                    foreach (var d in diags) _ = d.ToString();
                    if (diags.Count == 0)
                    {
                        Lint.Run(program);
                        CsBackend.EmitFiles(program);
                    }
                }
                catch (Exception e)
                {
                    crashes.Add($"{Path.GetRelativePath(dir, file).Replace('\\', '/')}: {name}: {e.GetType().Name}: {e.Message.Split('\n')[0]}");
                }
            }
        }
        return (tried, crashes);
    }
}
