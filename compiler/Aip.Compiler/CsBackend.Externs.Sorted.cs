namespace Aip;

sealed partial class CsBackend
{
    /// <summary>SortedMap and SortedSet externs (balanced tree).</summary>
    static void AddSortedExterns(Dictionary<string, string> d)
    {
        d["sortedMapEmpty"] = "new Aip.Persistent.PSorted(Rt.Order)";
        d["sortedMapSize"] = "(long)({0}).Count";
        d["sortedMapHas"] = "({0}).ContainsKey({1})";
        d["sortedMapGet"] = "Rt.SortedGet({0}, {1})";
        d["sortedMapSet"] = "({0}).Set({1}, {2})";
        d["sortedMapRemove"] = "({0}).Remove({1})";
        d["sortedMapKeys"] = "Aip.Persistent.PList.From({0})";
        d["sortedSetEmpty"] = "new Aip.Persistent.PSorted(Rt.Order)";
        d["sortedSetSize"] = "(long)({0}).Count";
        d["sortedSetHas"] = "({0}).ContainsKey({1})";
        d["sortedSetAdd"] = "({0}).Set({1}, true)";
        d["sortedSetRemove"] = "({0}).Remove({1})";
        d["sortedSetItems"] = "Aip.Persistent.PList.From({0})";
    }
}
