namespace Aip;

/// <summary>
/// The built-in generic types and how many type arguments each takes. One type per line, so branches that add
/// a type rarely touch the same line. The parser reads `Name<...>` for these and the checker validates them.
/// </summary>
static class GenericTypes
{
    public static readonly Dictionary<string, int> Arity = new()
    {
        ["List"] = 1,
        ["Map"] = 2,
        ["PriorityQueue"] = 1,
        ["Queue"] = 1,
        ["Set"] = 1,
        ["SortedMap"] = 2,
        ["SortedSet"] = 1,
    };
}
