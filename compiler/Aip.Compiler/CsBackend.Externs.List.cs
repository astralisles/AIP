namespace Aip;

sealed partial class CsBackend
{
    /// <summary>List externs.</summary>
    static void AddListExterns(Dictionary<string, string> d)
    {
        d["listEmpty"] = "Aip.Persistent.PList.Empty";
        d["listLength"] = "(long)({0}).Count";
        d["listGet"] = "({0})[(int)({1})]";
        d["listAppend"] = "({0}).Append({1})";
        d["listSet"] = "({0}).Set((int)({1}), {2})";
        d["listInsert"] = "({0}).Insert((int)({1}), {2})";
        d["listRemoveAt"] = "({0}).RemoveAt((int)({1}))";
        d["listConcat"] = "({0}).Concat({1})";
        d["listSlice"] = "({0}).Slice({1}, {2})";
        d["listReverse"] = "({0}).Reverse()";
        d["listIndexOf"] = "(long)({0}).IndexOf({1})";
        d["listContains"] = "({0}).Contains({1})";
        d["hashSha256"] = "Rt.HashSha256({0})";
        d["hashHmacSha256"] = "Rt.HashHmacSha256({0}, {1})";
        d["bytesFromText"] = "Rt.BytesFromText({0})";
        d["bytesToText"] = "Rt.BytesToText({0})";
        d["bytesToHex"] = "Rt.BytesToHex({0})";
        d["bytesFromHex"] = "Rt.BytesFromHex({0})";
        d["listSort"] = "Rt.ListSort({0}, {1})";
        d["listRange"] = "Rt.ListRange({0}, {1})";
    }
}
