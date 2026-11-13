using System.Globalization;

namespace Aip;

sealed partial class Interp
{
    /// <summary>Text, no-op.</summary>
    static object? CoreExtern(string name, List<object?> a)
    {
        switch (name)
        {
            case "noop": return null;
            case "textLength": return (long)((string)a[0]!).Length;
            case "textSlice":
                {
                    var s = (string)a[0]!;
                    int st = (int)Math.Clamp((long)a[1]!, 0, s.Length), en = (int)Math.Clamp((long)a[2]!, st, s.Length);
                    return s.Substring(st, en - st);
                }
            case "textCode": return (long)((string)a[0]!)[(int)(long)a[1]!];
            case "textFromCode": return ((char)(long)a[0]!).ToString();
            case "textToInt": return long.Parse((string)a[0]!, CultureInfo.InvariantCulture);
        }
        return NotHandled;
    }
}
