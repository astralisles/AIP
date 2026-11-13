namespace Aip;

sealed partial class Interp
{
    /// <summary>A function value (`&name`) in the interpreter.</summary>
    sealed record FnVal(Fn Target);

    /// <summary>The interpreter that is running, so a std function that takes a function value can call back into it.</summary>
    [ThreadStatic] static Interp? _active;

    object? CallValue(object? f, params object?[] args) => CallFn(((FnVal)f!).Target, args.ToList());

    /// <summary>listMap, listFilter, listFold, listMapPar (run one after the other: same result as the parallel back end).
    /// taskRun / taskAwait are effectful (`parallel`), so `aip test` refuses them.</summary>
    static object? FnExtern(string name, List<object?> a)
    {
        var self = _active ?? throw new Unsupported("no interpreter is running");
        List<object?> L(int i) => (List<object?>)a[i]!;
        switch (name)
        {
            case "listMap": case "listMapPar": return L(0).Select(x => self.CallValue(a[1], x)).ToList();
            case "listFilter": return L(0).Where(x => (bool)self.CallValue(a[1], x)!).ToList();
            case "listFold": { var acc = a[1]; foreach (var x in L(0)) acc = self.CallValue(a[2], acc, x); return acc; }
        }
        return NotHandled;
    }
}
