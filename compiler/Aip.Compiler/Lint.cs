using System.Globalization;

namespace Aip;

/// <summary>
/// Warnings for code that type-checks but is probably wrong. Runs only on a program that has no errors.
/// Every warning is a bug-shaped thing the checker does not reject: `aip lint` fails when there is one.
/// </summary>
static class Lint
{
    public static IEnumerable<Expr> Children(Expr e) => e switch
    {
        TextLit t => t.Parts.OfType<Expr>(),
        Call c => c.Args,
        Field f => new[] { f.Target },
        MatchExpr m => m.Arms.Select(a => a.Body).Prepend(m.Subject),
        Unary u => new[] { u.Operand },
        Binary b => new[] { b.Left, b.Right },
        IfExpr i => new[] { i.Cond, i.Then, i.Else },
        BlockExpr k => k.Stmts,
        _ => Array.Empty<Expr>(),
    };

    public static IEnumerable<Expr> All(Expr e)
    {
        yield return e;
        foreach (var c in Children(e)) foreach (var x in All(c)) yield return x;
    }

    static IEnumerable<Expr> BodyExprs(Fn fn) => fn.Body.SelectMany(s => All(s.Value));
    static IEnumerable<Expr> ContractExprs(Fn fn) => fn.Requires.Concat(fn.Ensures).Concat(fn.Examples).SelectMany(All);

    public static List<Diagnostic> Run(ProgramModel program)
    {
        var warnings = new List<Diagnostic>();
        void Warn(Fn fn, int line, string code, string message, string? fix = null) =>
            warnings.Add(new Diagnostic(fn.File, line, code, message, fix) { Kind = "warning" });

        foreach (var fn in program.ProjectFns)
        {
            var body = BodyExprs(fn).ToList();
            var all = body.Concat(ContractExprs(fn)).ToList();

            // unused-param: a parameter nothing refers to, in the body or in a contract.
            var names = all.OfType<NameRef>().Select(n => n.Name).ToHashSet();
            foreach (var p in fn.Params.Where(p => !fn.IsExtern && !names.Contains(p.Name)))
                Warn(fn, fn.Line, "unused-param", $"parameter `{p.Name}` of `{fn.Name}` is never used", "remove it, or use it");

            foreach (var e in all)
            {
                // constant-condition: an `if` whose condition never changes.
                if (e is IfExpr { Cond: BoolLit bl } i)
                    Warn(fn, i.Line, "constant-condition", $"this `if` condition is always {(bl.Value ? "true" : "false")}", "keep only the branch that runs");
                // self-comparison: `x == x` is always true, `x < x` always false.
                if (e is Binary { Op: "==" or "!=" or "<" or ">" or "<=" or ">=" } b && Same(b.Left, b.Right))
                    Warn(fn, b.Line, "self-comparison", $"both sides of `{b.Op}` are the same expression", "compare two different values");
                // division-by-zero: a literal zero divisor.
                if (e is Binary { Op: "/" or "%", Right: IntLit { Value: 0 } } d)
                    Warn(fn, d.Line, "division-by-zero", $"`{d.Op} 0` always fails at run time");
                // literal-out-of-range: a literal argument outside the range its parameter declares.
                if (e is Call { Ctor: null } c && program.Fns.TryGetValue(c.Name, out var target))
                {
                    // requires-always-false: every argument is a constant and a `requires` of the callee is false for them (T-0950).
                    if (Interp.FirstBrokenRequires(program, target, c.Args) is { } broken)
                        Warn(fn, c.Line, "requires-always-false", $"call `{c.Name}({broken.Args})` breaks `requires` of `{c.Name}` ({Path.GetFileName(target.File)}:{broken.Requires.Line}) and always fails at run time", "change the arguments, or the `requires`");
                    for (int k = 0; k < c.Args.Count && k < target.Params.Count; k++)
                    {
                        var range = target.Params[k].Type.Range;
                        if (range == "") continue;
                        long? n = c.Args[k] switch { IntLit il => il.Value <= long.MaxValue ? (long?)(long)il.Value : null, TextLit { Parts: [string s] } => s.Length, TextLit { Parts.Count: 0 } => 0, _ => null };
                        if (n == null) continue;
                        var parts = range.Split("..");
                        if ((parts[0] != "" && n < long.Parse(parts[0], CultureInfo.InvariantCulture)) || (parts[1] != "" && n > long.Parse(parts[1], CultureInfo.InvariantCulture)))
                            Warn(fn, c.Line, "literal-out-of-range", $"argument `{target.Params[k].Name}` of `{c.Name}` is {(c.Args[k] is IntLit ? n : "a text of length " + n)}, outside {range}");
                    }
                }
            }

            // always-true/false-example is impossible to see statically; `aip test` runs examples instead.
        }

        // unbounded-recursion: a cycle of calls where no function ever branches (`if`/`match`), so it can never stop.
        var straight = program.ProjectFns.Where(f => !BodyExprs(f).Any(e => e is IfExpr or MatchExpr)).ToDictionary(f => f.Name);
        var state = new Dictionary<string, int>();
        var reported = new HashSet<string>();
        bool Dfs(string name, Stack<string> path)
        {
            state[name] = 1; path.Push(name);
            foreach (var next in BodyExprs(straight[name]).OfType<Call>().Where(c => c.Ctor == null).Select(c => c.Name).Distinct().Where(straight.ContainsKey).OrderBy(x => x, StringComparer.Ordinal))
            {
                if (state.GetValueOrDefault(next) == 1)
                {
                    var cycle = path.Reverse().SkipWhile(x => x != next).ToList();
                    bool fresh = false;
                    foreach (var member in cycle) fresh |= reported.Add(member);
                    var fn = straight[next];
                    if (fresh) Warn(fn, fn.Line, "unbounded-recursion", $"`{string.Join("` -> `", cycle.Append(next))}` calls itself and nothing in it branches, so it never stops", "add an `if` or `match` that ends the recursion");
                }
                else if (state.GetValueOrDefault(next) == 0) Dfs(next, path);
            }
            path.Pop(); state[name] = 2;
            return true;
        }
        foreach (var name in straight.Keys.OrderBy(x => x, StringComparer.Ordinal))
            if (state.GetValueOrDefault(name) == 0) Dfs(name, new Stack<string>());
        return warnings;
    }

    static bool Same(Expr a, Expr b) => (a, b) switch
    {
        (NameRef x, NameRef y) => x.Name == y.Name,
        (IntLit x, IntLit y) => x.Value == y.Value,
        (BoolLit x, BoolLit y) => x.Value == y.Value,
        (Field x, Field y) => x.Name == y.Name && Same(x.Target, y.Target),
        _ => false,
    };
}
