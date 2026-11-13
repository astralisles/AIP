namespace Aip;

/// <summary>Function values in the C# back end: a `System.Func&lt;object[], object&gt;` that unboxes its arguments and boxes its result.
/// One uniform delegate type keeps type-erased std functions (listMap, ...) simple.</summary>
sealed partial class CsBackend
{
    static string EmitFnRef(FnRef r)
    {
        var args = string.Join(", ", r.ParamTypes.Select((t, i) => $"({CsType(t)})a[{i}]"));
        var call = $"Fn.@{r.Name}({args})";
        var body = r.Ret == "Unit" ? $"{{ {call}; return null; }}" : $"(object){call}";
        return $"new System.Func<object[], object>(a => {body})";
    }

    static string EmitIndirect(Call c)
    {
        var call = $"@{c.Name}(new object[] {{ {string.Join(", ", c.Args.Select(a => $"(object){Emit(a)}"))} }})";
        return c.Type is "Unit" ? call : Cast(true, c.Type, call);
    }
}
