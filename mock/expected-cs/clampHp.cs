public static partial class Fn
{
    public static int clampHp(int hp) => Math.Min(100, Math.Max(0, hp));
}
