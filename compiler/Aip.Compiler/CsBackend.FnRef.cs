namespace Aip;

/// <summary>Function values in the C# back end: a `System.Func&lt;object[], object&gt;` that unboxes its arguments and boxes its result.
/// One uniform delegate type keeps type-erased std functions (listMap, ...) simple.</summary>
sealed partial class CsBackend
{
    static string EmitIndirect(Call c)
    {
        var call = $"@{c.Name}(new object[] {{ {string.Join(", ", c.Args.Select(a => $"(object){Emit(a)}"))} }})";
        return c.Type is "Unit" ? call : Cast(true, c.Type, call);
    }
}
