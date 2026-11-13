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
        d["listSet"] = "Rt.ListSet({0}, {1}, {2})";
        d["listInsert"] = "Rt.ListInsert({0}, {1}, {2})";
        d["listRemoveAt"] = "Rt.ListRemoveAt({0}, {1})";
        d["listConcat"] = "Rt.ListConcat({0}, {1})";
        d["listSlice"] = "Rt.ListSlice({0}, {1}, {2})";
        d["listReverse"] = "Rt.ListReverse({0})";
        d["listIndexOf"] = "(long)({0}).IndexOf({1})";
        d["listContains"] = "({0}).Contains({1})";
        d["hashSha256"] = "Rt.HashSha256({0})";
        d["hashHmacSha256"] = "Rt.HashHmacSha256({0}, {1})";
        d["bytesFromText"] = "Rt.BytesFromText({0})";
        d["bytesToText"] = "Rt.BytesToText({0})";
        d["bytesToHex"] = "Rt.BytesToHex({0})";
        d["bytesFromHex"] = "Rt.BytesFromHex({0})";
        d["listRange"] = "Rt.ListRange({0}, {1})";
    }
}
