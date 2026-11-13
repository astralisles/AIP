using System.Globalization;

namespace Aip;

sealed partial class Interp
{
    /// <summary>List.</summary>
    static object? ListExtern(string name, List<object?> a)
    {
        List<object?> L(int i) => (List<object?>)a[i]!;
        switch (name)
        {
            case "listEmpty": return new List<object?>();
            case "listLength": return (long)L(0).Count;
            case "listGet": return L(0)[(int)(long)a[1]!];
            case "listAppend": return new List<object?>(L(0)) { a[1] };
            case "listSet": { var r = new List<object?>(L(0)); r[(int)(long)a[1]!] = a[2]; return r; }
            case "listInsert": { var r = new List<object?>(L(0)); r.Insert((int)(long)a[1]!, a[2]); return r; }
            case "listRemoveAt": { var r = new List<object?>(L(0)); r.RemoveAt((int)(long)a[1]!); return r; }
            case "listConcat": return L(0).Concat(L(1)).ToList();
            case "listSlice":
                {
                    int st = (int)Math.Clamp((long)a[1]!, 0, L(0).Count), en = (int)Math.Clamp((long)a[2]!, st, L(0).Count);
                    return L(0).GetRange(st, en - st);
                }
            case "listReverse": { var r = new List<object?>(L(0)); r.Reverse(); return r; }
            case "listIndexOf": return (long)L(0).FindIndex(x => Equals(x, a[1]));
            case "listContains": return L(0).Any(x => Equals(x, a[1]));
            case "listRange": { var r = new List<object?>(); for (long i = (long)a[0]!; i < (long)a[1]!; i++) r.Add(i); return r; }
        }
        return NotHandled;
    }
}
