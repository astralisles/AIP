using System.Globalization;

namespace Aip;

sealed partial class Interp
{
    /// <summary>Map and Set.</summary>
    static object? MapExtern(string name, List<object?> a)
    {
        Dictionary<object, object?> M(int i) => (Dictionary<object, object?>)a[i]!;
        switch (name)
        {
            case "mapEmpty" or "setEmpty": return new Dictionary<object, object?>();
            case "mapSize" or "setSize": return (long)M(0).Count;
            case "mapHas" or "setHas": return M(0).ContainsKey(a[1]!);
            case "mapGet": return M(0).TryGetValue(a[1]!, out var v) ? v : throw new KeyNotFoundException("mapGet: key not in the map");
            case "mapSet": return new Dictionary<object, object?>(M(0)) { [a[1]!] = a[2] };
            case "setAdd": return new Dictionary<object, object?>(M(0)) { [a[1]!] = true };
            case "mapRemove" or "setRemove": return M(0).Where(p => !p.Key.Equals(a[1])).ToDictionary(p => p.Key, p => p.Value);
            case "mapKeys" or "setItems": return M(0).Keys.Select(k => (object?)k).ToList();
        }
        return NotHandled;
    }
}
