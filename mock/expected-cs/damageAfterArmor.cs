public static partial class Fn
{
    public static int damageAfterArmor(Armor a, int dmg) => Math.Max(0, dmg - armorRating(a.kind));
}
