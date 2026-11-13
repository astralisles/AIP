using System.Linq;
namespace Aip;

sealed partial class Interp
{
    /// <summary>Bytes helpers. Console and network externs are effectful and refused by `aip test`.</summary>
    static object? NetExtern(string name, List<object?> a)
    {
        List<object?> L(int i) => (List<object?>)a[i]!;
        switch (name)
        {
                case "bytesEmpty": return Array.Empty<byte>();
                case "bytesEncode": return new System.Text.UTF8Encoding(false).GetBytes((string)a[0]!);
                case "bytesDecode": return new System.Text.UTF8Encoding(false).GetString((byte[])a[0]!);
                case "bytesLength": return (long)((byte[])a[0]!).Length;
                case "bytesGet": return (long)((byte[])a[0]!)[(int)(long)a[1]!];
                case "bytesSlice":
                    {
                        var d = (byte[])a[0]!;
                        int st = (int)Math.Clamp((long)a[1]!, 0, d.Length), en = (int)Math.Clamp((long)a[2]!, st, d.Length);
                        return d[st..en];
                    }
                case "bytesJoin": return ((byte[])a[0]!).Concat((byte[])a[1]!).ToArray();
                case "bytesFromList": return L(0).Select(x => checked((byte)(long)x!)).ToArray();
        }
        return NotHandled;
    }
}
