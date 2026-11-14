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
                case "bytesEncode": return new System.Text.UTF8Encoding(false).GetBytes((string)a[0]!).Select(x => (object?)x).ToList();
                case "bytesDecode": return new System.Text.UTF8Encoding(false).GetString(L(0).Select(x => (byte)x!).ToArray());
                case "bytesFromList": return L(0).Select(x => (object?)checked((byte)(long)x!)).ToList();
        }
        return NotHandled;
    }
}
