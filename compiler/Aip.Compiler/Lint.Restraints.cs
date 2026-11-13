using System.Globalization;

namespace Aip;

/// <summary>Warnings for function restraints (T-0966): `fails` that is not declared, and `cannot-fail` that is not shown.</summary>
static partial class Lint
{
    static void Restraints(ProgramModel program, Fn fn, Action<Fn, int, string, string, string?> Warn)
    {
        if (fn.IsExtern || fn.IsConst) return;
        var calls = BodyExprs(fn).OfType<Call>().Where(c => c.Ctor == null && program.Fns.ContainsKey(c.Name)).ToList();
        // hidden-failure: calls something that can crash, but the signature does not say so.
        if (!fn.Fails && !fn.CannotFail)
            foreach (var c in calls.Where(c => program.Fns[c.Name].Fails))
                Warn(fn, c.Line, "hidden-failure", $"`{fn.Name}` calls `{c.Name}`, which can fail, but `{fn.Name}` is not marked `fails`", $"add the header line `fails` to `{fn.Name}`");
        if (!fn.CannotFail) return;

        // cannot-fail-unproved: things the checker cannot yet show safe. Only warnings: unproved is not wrong (T-0950).
        foreach (var d in BodyExprs(fn).OfType<Binary>().Where(b => b.Op is "/" or "%" && b.Right is not IntLit { Value: > 0 }))
            Warn(fn, d.Line, "cannot-fail-unproved", $"the divisor of `{d.Op}` is not a non-zero number written in the code, so it may be 0", "divide only by a literal, or remove `cannot-fail`");
        foreach (var c in calls.Where(c => !program.Fns[c.Name].Fails))
        {
            var target = program.Fns[c.Name];
            if (!target.IsStd && !target.CannotFail && c.Name != fn.Name)
            {
                Warn(fn, c.Line, "cannot-fail-unproved", $"`{c.Name}` is not marked `cannot-fail`, so `{fn.Name}` cannot rely on it", $"mark `{c.Name}` as `cannot-fail`, or remove the call");
                continue;
            }
            bool needsProof = target.Requires.Count > 0 || target.Params.Any(p => p.Type.Range != "");
            if (!needsProof || ArgsProved(program, fn, target, c)) continue;
            Warn(fn, c.Line, "cannot-fail-unproved", $"cannot show the arguments of `{c.Name}` meet its `requires` or ranges", "call it with constants, or guard the call with an `if` on the parameter");
        }
    }

    static long Num(string s) => long.Parse(s, CultureInfo.InvariantCulture);

    static bool ArgsProved(ProgramModel program, Fn fn, Fn target, Call c)
    {
        if (c.Args.Count != target.Params.Count) return false;
        bool allLiteral = c.Args.All(a => a is IntLit or BoolLit or TextLit { Parts.Count: 0 } or TextLit { Parts: [string] });
        if (allLiteral)
        {
            if (Interp.FirstBrokenRequires(program, target, c.Args) != null) return false;
            for (int k = 0; k < c.Args.Count; k++)
            {
                var range = target.Params[k].Type.Range;
                if (range == "") continue;
                long? n = c.Args[k] switch { IntLit il => il.Value <= long.MaxValue ? (long?)(long)il.Value : null, TextLit { Parts: [string s] } => s.Length, TextLit => 0, _ => null };
                var parts = range.Split("..");
                if (n == null || (parts[0] != "" && n < Num(parts[0])) || (parts[1] != "" && n > Num(parts[1]))) return false;
            }
            return true;
        }
        // A recursive call `f(n - k)` for the `decreases n` parameter, where the `if` conditions around the call keep n - k inside the range of n.
        if (target != fn || fn.Decreases is not NameRef dn || target.Requires.Count > 0) return false;
        int idx = fn.Params.FindIndex(p => p.Name == dn.Name);
        if (idx < 0 || c.Args[idx] is not Binary { Op: "-", Right: IntLit { Value: > 0 } stepLit }) return false;
        long step = (long)stepLit.Value;
        for (int k = 0; k < c.Args.Count; k++)
            if (k != idx && !(c.Args[k] is NameRef same && same.Name == fn.Params[k].Name)) return false;
        var low = fn.Params[idx].Type.Range.Split("..")[0];
        if (low == "" || fn.Params[idx].Type.Range.Split("..")[1] != "") return false;
        foreach (var s in fn.Body)
            if (LowerBoundAt(s.Value, c, dn.Name, Num(low), out var atCall))
                return atCall - step >= Num(low);
        return false;
    }

    /// <summary>Finds `target` inside `e` and reports the smallest value `p` can have there, from the `if` conditions on the way.</summary>
    static bool LowerBoundAt(Expr e, Call target, string p, long lo, out long atCall)
    {
        if (ReferenceEquals(e, target)) { atCall = lo; return true; }
        if (e is IfExpr i)
        {
            long thenLo = lo, elseLo = lo;
            if (i.Cond is Binary { Left: NameRef { Name: var n }, Right: IntLit { Value: <= long.MaxValue } lit } b && n == p)
            {
                long r = (long)lit.Value;
                switch (b.Op)
                {
                    case ">": thenLo = Math.Max(lo, r + 1); break;
                    case ">=": thenLo = Math.Max(lo, r); break;
                    case "==": if (r == lo) elseLo = lo + 1; break;
                    case "!=": if (r == lo) thenLo = lo + 1; break;
                    case "<": elseLo = Math.Max(lo, r); break;
                    case "<=": elseLo = Math.Max(lo, r + 1); break;
                }
            }
            foreach (var (child, l) in new[] { (i.Cond, lo), (i.Then, thenLo), (i.Else, elseLo) })
                if (LowerBoundAt(child, target, p, l, out atCall)) return true;
            atCall = 0;
            return false;
        }
        foreach (var child in Children(e))
            if (LowerBoundAt(child, target, p, lo, out atCall)) return true;
        atCall = 0;
        return false;
    }
}
