namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Map and Set externs (hash based).</summary>
    static void AddMapExterns(Dictionary<string, string> d)
    {
        d["mapEmpty"] = "new System.Collections.Generic.Dictionary<object, object>()";
        d["mapSize"] = "(long)({0}).Count";
        d["mapHas"] = "({0}).ContainsKey({1})";
        d["mapGet"] = "Rt.MapGet({0}, {1})";
        d["mapSet"] = "Rt.MapSet({0}, {1}, {2})";
        d["mapRemove"] = "Rt.MapRemove({0}, {1})";
        d["mapKeys"] = "new System.Collections.Generic.List<object>(({0}).Keys)";
        d["setEmpty"] = "new System.Collections.Generic.Dictionary<object, object>()";
        d["setSize"] = "(long)({0}).Count";
        d["setHas"] = "({0}).ContainsKey({1})";
        d["setAdd"] = "Rt.MapSet({0}, {1}, true)";
        d["setRemove"] = "Rt.MapRemove({0}, {1})";
        d["setItems"] = "new System.Collections.Generic.List<object>(({0}).Keys)";
    }
}
