public static partial class Fn
{
    public static Player applyDamage(Player p, int dmg)
    {
        var d = damageAfterArmor(p.armor, dmg);
        return p with { hp = clampHp(p.hp - d) };
    }
}
