namespace Aip;

/// <summary>
/// Answers questions about a project from the checked model. Nothing is stored: every call
/// re-reads the sources, so the answer can never be stale.
/// </summary>
static class Query
{
    const string Verbs = "verbs: list | fn NAME | find WORDS | callers NAME | calls NAME | effect NAME | type NAME | users TYPE";

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
            case "type":
                if (!p.Types.TryGetValue(arg, out var td)) return NoType(p, arg);
                Console.WriteLine($"{(td.IsVariant ? "variant" : "struct")} {td.Name}{(td.Params.Count > 0 ? "<" + string.Join(", ", td.Params) + ">" : "")}");
                Console.WriteLine($"  {td.Doc}");
                Console.WriteLine($"  at {Path.GetRelativePath(p.Dir, td.File).Replace('\\', '/')}:{td.Line}");
                foreach (var cs in td.Cases) Console.WriteLine($"  {(td.IsVariant ? "case " : "fields ")}{cs.Name}({string.Join(", ", cs.Fields.Select(x => $"{x.Name} {x.Type}"))})");
                Console.WriteLine($"  used by: {List(TypeUsers(p, td).Select(u => u.Name))}");
                return 0;
            case "users":
                if (!p.Types.TryGetValue(arg, out var ut)) return NoType(p, arg);
                var users = TypeUsers(p, ut).ToList();
                foreach (var u in users) Console.WriteLine(u.Line);
                if (users.Count == 0) Console.WriteLine("no match");
                return 0;
            case "effect":
                if (arg == "") return Usage();
                hits = all.Where(g => arg == "pure" ? g.Effects.Count == 0 : g.Effects.Any(e => e == arg || e.StartsWith(arg + ".", StringComparison.Ordinal)));
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
        foreach (var st in f.Body) WalkAll(st.Value, e => { if (e is Call c && c.Ctor == null) set.Add(c.Name); });
        return set;
    }

    // Walks every expression of a body, including match arms, field reads and blocks.
    static void WalkAll(Expr? e, Action<Expr> visit)
    {
        if (e == null) return;
        visit(e);
        switch (e)
        {
            case Call c: foreach (var x in c.Args) WalkAll(x, visit); break;
            case Unary u: WalkAll(u.Operand, visit); break;
            case Binary b: WalkAll(b.Left, visit); WalkAll(b.Right, visit); break;
            case IfExpr i: WalkAll(i.Cond, visit); WalkAll(i.Then, visit); WalkAll(i.Else, visit); break;
            case TextLit t: foreach (var x in t.Parts.OfType<Expr>()) WalkAll(x, visit); break;
            case Field f: WalkAll(f.Target, visit); break;
            case MatchExpr m: WalkAll(m.Subject, visit); foreach (var a in m.Arms) WalkAll(a.Body, visit); break;
            case BlockExpr bl: foreach (var x in bl.Stmts) WalkAll(x, visit); break;
        }
    }

    static bool Mentions(string text, string name) =>
        System.Text.RegularExpressions.Regex.IsMatch(text, $@"(?<![A-Za-z0-9_]){System.Text.RegularExpressions.Regex.Escape(name)}(?![A-Za-z0-9_])");

    // Who uses a struct or variant: functions that name it in a signature, construct it, match on it, or read its
    // fields in the body, and other types that have a field of it. Std functions are not listed.
    static IEnumerable<(string Name, string Line)> TypeUsers(ProgramModel p, TypeDef t)
    {
        var names = new HashSet<string>(t.Cases.Select(c => c.Name)) { t.Name };
        foreach (var g in p.ProjectFns)
        {
            var how = new SortedSet<string>(StringComparer.Ordinal);
            if (g.Params.Any(x => Mentions(x.Type.Name, t.Name))) how.Add("parameter");
            if (Mentions(g.Ret.Name, t.Name)) how.Add("returns");
            foreach (var st in g.Body)
                WalkAll(st.Value, e =>
                {
                    if (e is Call c && c.Ctor != null && names.Contains(c.Name)) how.Add("constructs");
                    else if (e is MatchExpr m && Mentions(m.Subject.Type, t.Name)) how.Add("matches");
                    else if (e is Field f && Mentions(f.Target.Type, t.Name)) how.Add("reads field");
                });
            if (how.Count > 0) yield return (g.Name, $"{g.Signature} | {g.EffectsText} | {string.Join(", ", how)}");
        }
        foreach (var o in p.Types.Values.Where(o => o.Name != t.Name).OrderBy(o => o.Name, StringComparer.Ordinal))
            if (o.Cases.Any(c => c.Fields.Any(x => Mentions(x.Type.Name, t.Name))))
                yield return (o.Name, $"{(o.IsVariant ? "variant" : "struct")} {o.Name} | has a field of this type");
    }

    static int NoType(ProgramModel p, string name)
    {
        var near = p.Types.Keys.Where(k => k.Contains(name, StringComparison.OrdinalIgnoreCase)).OrderBy(k => k, StringComparer.Ordinal).Take(5);
        Console.WriteLine($"error no-such-type: `{name}`{(near.Any() ? "; did you mean " + string.Join(", ", near) + "?" : "")}");
        return 1;
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
