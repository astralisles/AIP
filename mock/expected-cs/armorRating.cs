public static partial class Fn
{
    public static int armorRating(ArmorKind kind) => kind switch
    {
        ArmorKind.None => 0,
        ArmorKind.Leather => 2,
        ArmorKind.Chain => 5,
        ArmorKind.Plate => 8,
        _ => throw new System.Diagnostics.UnreachableException(),
    };
}
