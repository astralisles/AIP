namespace Aip;

sealed partial class Interp
{
    /// <summary>Float math, text parsing and the seeded generator. Clock, sleep and `randomSeed` are effectful and refused by `aip test`.</summary>
    static object? TimeExtern(string name, List<object?> a)
    {
        double D(int i) => (double)a[i]!;
        switch (name)
        {
            case "mathSqrt": return Math.Sqrt(D(0));
            case "mathFloor": return Math.Floor(D(0));
            case "mathCeil": return Math.Ceiling(D(0));
            case "mathRound": return Math.Round(D(0), MidpointRounding.AwayFromZero);
            case "mathSin": return Math.Sin(D(0));
            case "mathCos": return Math.Cos(D(0));
            case "mathTan": return Math.Tan(D(0));
            case "mathExp": return Math.Exp(D(0));
            case "mathLog": return Math.Log(D(0));
            case "mathAbs": return Math.Abs(D(0));
            case "mathPow": return Math.Pow(D(0), D(1));
            case "mathAtan2": return Math.Atan2(D(0), D(1));
            case "mathMin": return Math.Min(D(0), D(1));
            case "mathMax": return Math.Max(D(0), D(1));
            case "mathClamp": return D(1) > D(2) ? throw new ArgumentException("low is above high") : Math.Min(Math.Max(D(0), D(1)), D(2));
            case "mathLerp": return D(0) + (D(1) - D(0)) * D(2);
            case "mathIsNaN": return double.IsNaN(D(0));
            case "mathPi": return Math.PI;
            case "textToFloat": return double.Parse((string)a[0]!, System.Globalization.CultureInfo.InvariantCulture);
            case "randomNext": return unchecked((long)a[0]! + (long)0x9E3779B97F4A7C15UL);
            case "randomRange": return (long)a[1]! <= 0 ? throw new ArgumentException("bound must be above 0") : (long)(Mix((long)a[0]!) % (ulong)(long)a[1]!);
            case "randomFloat": return (Mix((long)a[0]!) >> 11) * (1.0 / 9007199254740992.0);
        }
        return NotHandled;
    }

    static ulong Mix(long s)
    {
        unchecked
        {
            ulong z = (ulong)s;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
