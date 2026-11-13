namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Map and Set externs (hash based).</summary>
    static void AddMapExterns(Dictionary<string, string> d)
    {
        d["mapEmpty"] = "Aip.Persistent.PMap.Empty";
        d["mapSize"] = "(long)({0}).Count";
        d["mapHas"] = "({0}).ContainsKey({1})";
        d["mapGet"] = "Rt.MapGet({0}, {1})";
        d["mapSet"] = "({0}).Set({1}, {2})";
        d["mapRemove"] = "({0}).Remove({1})";
        d["mapKeys"] = "({0}).Keys()";
        d["setEmpty"] = "Aip.Persistent.PMap.Empty";
        d["setSize"] = "(long)({0}).Count";
        d["setHas"] = "({0}).ContainsKey({1})";
        d["setAdd"] = "({0}).Set({1}, true)";
        d["setRemove"] = "({0}).Remove({1})";
        d["setItems"] = "({0}).Keys()";
    }
}
