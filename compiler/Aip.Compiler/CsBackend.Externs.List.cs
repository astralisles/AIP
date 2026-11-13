namespace Aip;

sealed partial class CsBackend
{
    /// <summary>List externs.</summary>
    static void AddListExterns(Dictionary<string, string> d)
    {
        d["listEmpty"] = "new System.Collections.Generic.List<object>()";
        d["listLength"] = "(long)({0}).Count";
        d["listGet"] = "({0})[(int)({1})]";
        d["listAppend"] = "Rt.Append({0}, {1})";
    }
}
