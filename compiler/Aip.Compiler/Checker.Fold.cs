namespace Aip;

/// <summary>Checker: extra arguments of an accumulating function are a left fold (T-1037, T-1040).</summary>
sealed partial class Checker
{
    /// <summary>`f(x, a, b, c)` is shorthand for `f(f(f(x, a), b), c)` when `f` takes two parameters and returns the type of its
    /// first one (`listAppend(List&lt;T&gt;, T) -> List&lt;T&gt;`). The signature decides, so no list of functions is kept.
    /// Rewrites the call in place; a call that does not fit is left for the normal argument count error.</summary>
    void FoldExtraArgs(Call c, Dictionary<string, string> env)
    {
        if (c.Args.Count <= 2 || env.ContainsKey(c.Name)) return;
        if (!program.Fns.TryGetValue(c.Name, out var target) || target.IsConst || target.Params.Count != 2) return;
        if (target.Params[0].Type.Name != target.Ret.Name) return;
        var rest = c.Args.Skip(2).ToList();
        c.Args.RemoveRange(2, c.Args.Count - 2);
        foreach (var a in rest)
            c = Wrap(c, a);
    }

    static Call Wrap(Call inner, Expr next)
    {
        // `inner` keeps its identity (the caller's node): move its content into a new node and make `inner` the outer call.
        var moved = new Call { Line = inner.Line, Name = inner.Name, Args = inner.Args };
        inner.Args = new List<Expr> { moved, next };
        return inner;
    }
}
