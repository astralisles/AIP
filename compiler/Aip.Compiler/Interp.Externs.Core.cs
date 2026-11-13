using System.Globalization;
using System.Numerics;

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
            case "numToI8" or "numToI16" or "numToI32" or "numToU8" or "numToU16" or "numToU32" or "numToU64":
                return Fit(Big(a[0]!), name[5..], $"`{name}`");
            case "numFromI8" or "numFromI16" or "numFromI32" or "numFromU8" or "numFromU16" or "numFromU32" or "numFromU64":
                return Fit(Big(a[0]!), "I64", $"`{name}`");
            case "numToF32": return (float)(long)a[0]!;
            case "numToF64": return (double)(long)a[0]!;
            case "numFromF32" or "numFromF64":
                {
                    double d = ToDouble(a[0]!);
                    if (double.IsNaN(d) || d >= 9223372036854775808.0 || d < -9223372036854775808.0) throw new OverflowException($"{d} does not fit in I64");
                    return (long)d;
                }
            case "numF32ToF64": return (double)(float)a[0]!;
            case "numF64ToF32": return (float)(double)a[0]!;
            case "bitAnd" or "bitOr" or "bitXor" or "bitNot" or "bitShl" or "bitShr":
                {
                    string ty = a[0] switch { long => "I64", sbyte => "I8", short => "I16", int => "I32", byte => "U8", ushort => "U16", uint => "U32", _ => "U64" };
                    var (_, max) = Limits[ty];
                    int width = ty switch { "I64" or "U64" => 64, "I32" or "U32" => 32, "I16" or "U16" => 16, _ => 8 };
                    BigInteger mod = BigInteger.One << width, x = Big(a[0]!);
                    BigInteger Wrap(BigInteger v) { v = ((v % mod) + mod) % mod; return v > max ? v - mod : v; }
                    BigInteger res;
                    if (name is "bitShl" or "bitShr")
                    {
                        long n = (long)Big(a[1]!);
                        if (n < 0 || n >= width) throw new InvalidOperationException($"shift count {n} must be 0 to {width - 1}");
                        res = name == "bitShl" ? Wrap(x << (int)n) : x >> (int)n;
                    }
                    else if (name == "bitNot") res = Wrap(-x - 1);
                    else
                    {
                        BigInteger y = Big(a[1]!), ux = (x + mod) % mod, uy = (y + mod) % mod;
                        res = Wrap(name == "bitAnd" ? ux & uy : name == "bitOr" ? ux | uy : ux ^ uy);
                    }
                    return Fit(res, ty, name);
                }
            case "textFind": return (long)((string)a[0]!).IndexOf((string)a[1]!, StringComparison.Ordinal);
            case "textContains": return ((string)a[0]!).Contains((string)a[1]!, StringComparison.Ordinal);
            case "textStartsWith": return ((string)a[0]!).StartsWith((string)a[1]!, StringComparison.Ordinal);
            case "textEndsWith": return ((string)a[0]!).EndsWith((string)a[1]!, StringComparison.Ordinal);
            case "textTrim": return ((string)a[0]!).Trim();
            case "textReplace": return ((string)a[0]!).Replace((string)a[1]!, (string)a[2]!, StringComparison.Ordinal);
            case "textToUpper": return ((string)a[0]!).ToUpperInvariant();
            case "textToLower": return ((string)a[0]!).ToLowerInvariant();
            case "textCompare": return (long)Math.Sign(string.CompareOrdinal((string)a[0]!, (string)a[1]!));
            case "textFromInt": return ((long)a[0]!).ToString(CultureInfo.InvariantCulture);
            case "textRepeat": return string.Concat(Enumerable.Repeat((string)a[0]!, (int)(long)a[1]!));
            case "textSplit": return ((string)a[0]!).Split((string)a[1]!, StringSplitOptions.None).Select(x => (object?)x).ToList();
            case "textJoin": return string.Join((string)a[1]!, ((List<object?>)a[0]!).Cast<string>());
            case "intMin": return Math.Min((long)a[0]!, (long)a[1]!);
            case "intMax": return Math.Max((long)a[0]!, (long)a[1]!);
            case "intAbs": return Math.Abs((long)a[0]!);
            case "intSign": return (long)Math.Sign((long)a[0]!);
            case "intClamp": return Math.Clamp((long)a[0]!, (long)a[1]!, (long)a[2]!);
            case "intPow": return (long)System.Numerics.BigInteger.Pow((long)a[0]!, (int)(long)a[1]!);
            case "intGcd": return (long)System.Numerics.BigInteger.GreatestCommonDivisor((long)a[0]!, (long)a[1]!);
            case "intAnd": return (long)a[0]! & (long)a[1]!;
            case "intOr": return (long)a[0]! | (long)a[1]!;
            case "intXor": return (long)a[0]! ^ (long)a[1]!;
            case "intNot": return ~(long)a[0]!;
            case "intShiftLeft": return (long)a[0]! << (int)(long)a[1]!;
            case "intShiftRight": return (long)a[0]! >> (int)(long)a[1]!;        }
        return NotHandled;
    }
}
