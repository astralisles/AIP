namespace Aip;

sealed partial class CsBackend
{
    /// <summary>SortedMap and SortedSet externs (balanced tree).</summary>
    static void AddSortedExterns(Dictionary<string, string> d)
    {
        d["sortedMapEmpty"] = "new System.Collections.Generic.SortedDictionary<object, object>(Rt.Order)";
        d["sortedMapSize"] = "(long)({0}).Count";
        d["sortedMapHas"] = "({0}).ContainsKey({1})";
        d["sortedMapGet"] = "Rt.SortedGet({0}, {1})";
        d["sortedMapSet"] = "Rt.SortedPut({0}, {1}, {2})";
        d["sortedMapRemove"] = "Rt.SortedRemove({0}, {1})";
        d["sortedMapKeys"] = "new System.Collections.Generic.List<object>(({0}).Keys)";
        d["sortedSetEmpty"] = "new System.Collections.Generic.SortedDictionary<object, object>(Rt.Order)";
        d["sortedSetSize"] = "(long)({0}).Count";
        d["sortedSetHas"] = "({0}).ContainsKey({1})";
        d["sortedSetAdd"] = "Rt.SortedPut({0}, {1}, true)";
        d["sortedSetRemove"] = "Rt.SortedRemove({0}, {1})";
        d["sortedSetItems"] = "new System.Collections.Generic.List<object>(({0}).Keys)";
    }
}
