using System.Globalization;

namespace Aip;

sealed partial class Interp
{
    /// <summary>List.</summary>
    static object? ListExtern(string name, List<object?> a)
    {
        List<object?> L(int i) => (List<object?>)a[i]!;
        byte[] IntBytes(List<object?> l) => l.Select(x => { var v = (long)x!; if (v < 0 || v > 255) throw new InvalidOperationException("byte out of range 0..255: " + v); return (byte)v; }).ToArray();
        switch (name)
        {
            case "listEmpty": return new List<object?>();
            case "listLength": return (long)L(0).Count;
            case "listSort":
                {
                    var self = _active ?? throw new Unsupported("no interpreter is running");
                    return L(0).OrderBy(x => x, Comparer<object?>.Create((p, q) =>
                        (bool)self.CallValue(a[1], p, q)! ? -1 : (bool)self.CallValue(a[1], q, p)! ? 1 : 0)).ToList();
                }
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
            case "hashSha256": return System.Security.Cryptography.SHA256.HashData(IntBytes(L(0))).Select(x => (object?)(long)x).ToList();
            case "hashHmacSha256": return System.Security.Cryptography.HMACSHA256.HashData(IntBytes(L(0)), IntBytes(L(1))).Select(x => (object?)(long)x).ToList();
            case "bytesFromText": return System.Text.Encoding.UTF8.GetBytes((string)a[0]!).Select(x => (object?)(long)x).ToList();
            case "bytesToText": return System.Text.Encoding.UTF8.GetString(IntBytes(L(0)));
            case "bytesToHex": return Convert.ToHexString(IntBytes(L(0))).ToLowerInvariant();
            case "bytesFromHex":
                {
                    var h = (string)a[0]!;
                    if (h.Length % 2 != 0) throw new InvalidOperationException("hex text must have an even number of digits");
                    return Convert.FromHexString(h).Select(x => (object?)(long)x).ToList();
                }
            case "listReverse": { var r = new List<object?>(L(0)); r.Reverse(); return r; }
            case "listIndexOf": return (long)L(0).FindIndex(x => Equals(x, a[1]));
            case "listContains": return L(0).Any(x => Equals(x, a[1]));
            case "listRange": { var r = new List<object?>(); for (long i = (long)a[0]!; i < (long)a[1]!; i++) r.Add(i); return r; }
        }
        return NotHandled;
    }
}
