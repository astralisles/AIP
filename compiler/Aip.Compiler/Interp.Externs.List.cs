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
        }
        return NotHandled;
    }
}
