namespace Aip;

sealed partial class CsBackend
{
    const string RtTime = """
        public static partial class Rt
        {
            static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
            public static long TimeNow() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            public static long TimeMono() => Clock.ElapsedMilliseconds;
            public static void TimeSleep(long ms) { if (ms > 0) System.Threading.Thread.Sleep((int)System.Math.Min(ms, int.MaxValue)); }
            public static double MathClamp(double x, double lo, double hi) => lo > hi ? throw new System.ArgumentException("low is above high") : System.Math.Min(System.Math.Max(x, lo), hi);

            // SplitMix64: the state moves by a fixed odd step, the output is a scrambled copy of the state.
            public static long RandNext(long s) => unchecked(s + (long)0x9E3779B97F4A7C15UL);
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
            public static long RandRange(long s, long bound) => bound <= 0 ? throw new System.ArgumentException("bound must be above 0") : (long)(Mix(s) % (ulong)bound);
            public static double RandFloat(long s) => (Mix(s) >> 11) * (1.0 / 9007199254740992.0);
            public static long EntropySeed() => System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue) * 4294967296L + System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        }
        """;
}
