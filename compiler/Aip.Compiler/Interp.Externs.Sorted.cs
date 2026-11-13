using System.Globalization;

namespace Aip;

sealed partial class Interp
{
    sealed class OrderCmp : IComparer<object>
    {
        public int Compare(object? x, object? y) => x is string s ? string.CompareOrdinal(s, (string)y!) : ((long)x!).CompareTo((long)y!);
    }
    static readonly IComparer<object> Order = new OrderCmp();

    /// <summary>SortedMap and SortedSet.</summary>
    static object? SortedExtern(string name, List<object?> a)
    {
        SortedDictionary<object, object?> S(int i) => (SortedDictionary<object, object?>)a[i]!;
        SortedDictionary<object, object?> Copy(int i) => new(S(i), Order);
        switch (name)
        {
            case "sortedMapEmpty" or "sortedSetEmpty": return new SortedDictionary<object, object?>(Order);
            case "sortedMapSize" or "sortedSetSize": return (long)S(0).Count;
            case "sortedMapHas" or "sortedSetHas": return S(0).ContainsKey(a[1]!);
            case "sortedMapGet": return S(0).TryGetValue(a[1]!, out var sv) ? sv : throw new KeyNotFoundException("sortedMapGet: key not in the map");
            case "sortedMapSet": { var r = Copy(0); r[a[1]!] = a[2]; return r; }
            case "sortedSetAdd": { var r = Copy(0); r[a[1]!] = true; return r; }
            case "sortedMapRemove" or "sortedSetRemove": { var r = Copy(0); r.Remove(a[1]!); return r; }
            case "sortedMapKeys" or "sortedSetItems": return S(0).Keys.Select(k => (object?)k).ToList();
        }
        return NotHandled;
    }
}
