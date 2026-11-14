namespace Aip;

/// <summary>Checker: function values (`&name`, type `Fn<A,B,R>`) and calls through them (docs/design/function-pointers-T-0928.md).</summary>
sealed partial class Checker
{
    static bool IsFnType(string t) => t.StartsWith("Fn<", StringComparison.Ordinal) && Split(t) is { Name: "Fn" };

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
