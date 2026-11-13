namespace Aip;

/// <summary>Checker: function values (`&name`, type `Fn<A,B,R>`) and calls through them (docs/design/function-pointers-T-0928.md).</summary>
sealed partial class Checker
{
    static bool IsFnType(string t) => t.StartsWith("Fn<", StringComparison.Ordinal) && Split(t) is { Name: "Fn" };

    /// <summary>`&name`: the target must be a plain function (not a constant or constructor), pure, not generic, and not a std extern.</summary>
    string FnRefType(FnRef r)
    {
        if (!program.Fns.TryGetValue(r.Name, out var target) || target.IsConst)
        {
            if (target?.IsConst == true) Error(r.Line, "bad-fn-ref", $"`{r.Name}` is a constant, not a function", $"write `{r.Name}` without `&`");
            else Error(r.Line, "unknown-name", $"there is no function `{r.Name}`", Suggest(r.Name, program.Fns.Keys));
            return "";
        }
        _fn.Calls.Add(r.Name); // reachability: `&name` counts as a call from here
        if (target.Effects.Count > 0)
            Error(r.Line, "bad-fn-ref", $"`{r.Name}` has effects ({target.EffectsText}); only pure functions can be used as values", "call it directly, or wrap the pure part in its own function");
        else if (target.IsExtern)
            Error(r.Line, "bad-fn-ref", $"`{r.Name}` is a standard-library extern and cannot be used as a value yet", "write a small function that calls it, and use `&` on that");
        else if (target.Params.Concat(new[] { new Param("", target.Ret) }).Any(p => TypeVarsOf(p.Type.Name).Count > 0))
            Error(r.Line, "bad-fn-ref", $"`{r.Name}` is generic; a function value needs concrete types", "write a non-generic function that calls it");
        else { r.ParamTypes = target.Params.Select(p => p.Type.Name).ToList(); r.Ret = target.Ret.Name; return Join("Fn", target.Params.Select(p => p.Type.Name).Append(target.Ret.Name)); }
        return "";
    }

    static HashSet<string> TypeVarsOf(string t) { var s = new HashSet<string>(); TypeVars(t, s); return s; }

    /// <summary>`f(x)` where `f` is a local of type `Fn<..>`: checks the arguments against the type; the result is its last argument.</summary>
    string IndirectCallType(Call c, string fnType, List<string> argTypes)
    {
        c.Indirect = true;
        var all = Split(fnType)!.Value.Args;
        var want = all.Take(all.Count - 1).ToList();
        if (argTypes.Count != want.Count)
            Error(c.Line, "wrong-arguments", $"`{c.Name}` takes {want.Count} argument(s), found {argTypes.Count}", $"type: {fnType}");
        else
            for (int i = 0; i < want.Count; i++)
                if (!Unify(want[i], argTypes[i], new Dictionary<string, string>()) && !FitLiteral(c.Args[i], want[i]))
                    Error(c.Line, "type-mismatch", $"argument {i + 1} of `{c.Name}` must be {want[i]}, found {argTypes[i]}", $"type: {fnType}");
        return all[^1];
    }
}
