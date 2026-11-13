namespace Aip;

/// <summary>
/// Answers questions about a project from the checked model. Nothing is stored: every call
/// re-reads the sources, so the answer can never be stale.
/// </summary>
static class Query
{
    const string Verbs = "verbs: list | fn NAME | find WORDS | callers NAME | calls NAME | effect NAME";

    public static int Run(ProgramModel p, string[] a)
    {
        var verb = a.Length > 0 ? a[0] : "";
        var arg = string.Join(" ", a.Skip(1));
        var all = p.ProjectFns.ToList();
        IEnumerable<Fn> hits;
        switch (verb)
        {
            case "list":
                hits = all;
                break;
            case "fn":
                if (!p.Fns.TryGetValue(arg, out var f)) return NotFound(p, arg);
                Console.WriteLine($"{f.Signature} | {f.EffectsText}{(f.IsStd ? " | standard library" : "")}");
                Console.WriteLine($"  {f.Doc}");
                Console.WriteLine($"  at {Path.GetRelativePath(p.Dir, f.File).Replace('\\', '/')}:{f.Line}");
                foreach (var h in Header(f)) Console.WriteLine("  " + h);
                Console.WriteLine($"  calls: {List(BodyCalls(f))}");
                Console.WriteLine($"  called by: {List(all.Where(g => BodyCalls(g).Contains(f.Name)).Select(g => g.Name))}");
                return 0;
            case "find":
                var words = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0) return Usage();
                hits = all.Where(g => words.All(w => $"{g.Name} {g.Doc}".Contains(w, StringComparison.OrdinalIgnoreCase)));
                break;
            case "callers":
                if (!p.Fns.ContainsKey(arg)) return NotFound(p, arg);
                hits = all.Where(g => BodyCalls(g).Contains(arg));
                break;
            case "calls":
                if (!p.Fns.TryGetValue(arg, out var c)) return NotFound(p, arg);
                hits = BodyCalls(c).Where(p.Fns.ContainsKey).Select(n => p.Fns[n]).OrderBy(g => g.Name, StringComparer.Ordinal);
                break;
            case "effect":
                if (arg == "") return Usage();
                hits = all.Where(g => arg == "pure" ? g.Effects.Count == 0 : g.Effects.Any(e => e == arg || e.StartsWith(arg + ".")));
                break;
            default:
                return Usage();
        }
        var n = 0;
        foreach (var g in hits) { Console.WriteLine($"{g.Signature} | {g.EffectsText} | {g.Doc}"); n++; }
        if (n == 0) Console.WriteLine("no match");
        return 0;
    }

    // Functions called from the body only (Fn.Calls also holds calls made in examples and contracts).
    static SortedSet<string> BodyCalls(Fn f)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        void Walk(Expr e)
        {
            switch (e)
            {
                case Call c: set.Add(c.Name); foreach (var x in c.Args) Walk(x); break;
                case Unary u: Walk(u.Operand); break;
                case Binary b: Walk(b.Left); Walk(b.Right); break;
                case IfExpr i: Walk(i.Cond); Walk(i.Then); Walk(i.Else); break;
                case TextLit t: foreach (var x in t.Parts.OfType<Expr>()) Walk(x); break;
            }
        }
        foreach (var st in f.Body) Walk(st.Value);
        return set;
    }

    static string List(IEnumerable<string> names) { var l = names.ToList(); return l.Count == 0 ? "(none)" : string.Join(", ", l); }

    // The header is everything between the declaration line and `body`: purpose, effects, contracts, examples.
    static IEnumerable<string> Header(Fn f)
    {
        var lines = File.ReadAllLines(f.File);
        for (int i = f.Line; i < lines.Length && lines[i].Trim() != "body"; i++)
            if (i > f.Line && lines[i].Trim() != "") yield return lines[i].Trim();
    }

    static int NotFound(ProgramModel p, string name)
    {
        var near = p.Fns.Keys.Where(k => k.Contains(name, StringComparison.OrdinalIgnoreCase)).OrderBy(k => k, StringComparer.Ordinal).Take(5);
        Console.WriteLine($"error no-such-function: `{name}`{(near.Any() ? "; did you mean " + string.Join(", ", near) + "?" : "")}");
        return 1;
    }

    static int Usage() { Console.WriteLine(Verbs); return 2; }
}
