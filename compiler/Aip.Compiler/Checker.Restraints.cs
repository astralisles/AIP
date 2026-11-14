namespace Aip;

/// <summary>Function restraints (T-0966): `decreases` (termination), `cannot-fail` and `fails` (docs/design/function-restraints-T-0966.md).</summary>
sealed partial class Checker
{
    /// <summary>Runs after the body is checked, so calls are already told apart from constructors.</summary>
    void CheckRestraints(Fn fn)
    {
        if (fn.IsExtern || fn.IsConst) return;
        var calls = fn.Body.SelectMany(s => Lint.All(s.Value)).OfType<Call>().Where(c => c.Ctor == null).ToList();
        if (fn.Decreases != null) CheckDecreases(fn, calls);
        if (fn.CannotFail) CheckCannotFail(fn, calls);
    }

    /// <summary>`decreases p`: p is a whole-number parameter with a lower bound, and every recursive call passes `p - k` with k at least 1.</summary>
    void CheckDecreases(Fn fn, List<Call> calls)
    {
        var line = fn.DecreasesLine;
        var index = fn.Decreases is NameRef nr ? fn.Params.FindIndex(p => p.Name == nr.Name && IsInt(p.Type.Name)) : -1;
        if (index < 0)
        {
            Error(line, "bad-decreases", "`decreases` must name a whole-number parameter of this function", "write `decreases n` where `n` is an I64 parameter that shrinks in each recursive call");
            return;
        }
        var p = fn.Params[index];
        if (p.Type.Range.Split("..")[0] == "")
            Error(line, "decreases-unbounded", $"`{p.Name}` has no lower bound, so counting down never has to stop", $"give it a range, for example `{p.Name} {p.Type.Name} 0..`");
        foreach (var c in calls.Where(c => c.Name == fn.Name))
        {
            bool shrinks = index < c.Args.Count && c.Args[index] is Binary { Op: "-", Left: NameRef l, Right: IntLit { Value: > 0 } } && l.Name == p.Name;
            if (!shrinks)
                Error(c.Line, "decreases-not-shrinking", $"this call does not pass `{p.Name} - k` (k at least 1) for `{p.Name}`, so `decreases {p.Name}` is not shown to shrink", $"pass `{p.Name} - 1` (or a larger step), or remove `decreases`");
        }
    }

    /// <summary>`cannot-fail`: calls nothing marked `fails`, and recursion is only direct recursion that has a `decreases`.</summary>
    void CheckCannotFail(Fn fn, List<Call> calls)
    {
        foreach (var c in calls.Where(c => program.Fns.TryGetValue(c.Name, out var t) && t.Fails))
            Error(c.Line, "cannot-fail-calls-fails", $"`{fn.Name}` is `cannot-fail` but calls `{c.Name}`, which is marked `fails`", $"remove `cannot-fail`, mark `{fn.Name}` as `fails`, or avoid `{c.Name}`");
        bool Reaches(string from, HashSet<string> seen)
        {
            if (!program.Fns.TryGetValue(from, out var f) || f.IsExtern || !seen.Add(from)) return false;
            foreach (var n in f.Body.SelectMany(s => Lint.All(s.Value)).OfType<Call>().Select(c => c.Name).Distinct())
                if (n == fn.Name || Reaches(n, seen)) return true;
            return false;
        }
        bool direct = calls.Any(c => c.Name == fn.Name);
        bool indirect = calls.Select(c => c.Name).Where(n => n != fn.Name).Distinct().Any(n => Reaches(n, new HashSet<string>()));
        if (indirect)
            Error(fn.RestraintLine, "cannot-fail-recursion", $"`{fn.Name}` is `cannot-fail` but is part of a cycle of calls through other functions, which `decreases` cannot yet show to stop", "make the recursion direct, or remove `cannot-fail`");
        else if (direct && fn.Decreases == null)
            Error(fn.RestraintLine, "cannot-fail-recursion", $"`{fn.Name}` is `cannot-fail` but calls itself without a `decreases` line, so it may never stop", "add `decreases n` for the parameter that shrinks");
    }
}
