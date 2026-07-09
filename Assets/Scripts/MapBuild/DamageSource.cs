namespace Doom.MapBuild
{
    /// Who dealt the damage — for infighting retargeting.
    public readonly struct DamageSource
    {
        public readonly EnemyHealth MonsterAttacker; // null = игрок/среда
        DamageSource(EnemyHealth m) { MonsterAttacker = m; }
        public static DamageSource Player() => new DamageSource(null);
        public static DamageSource Monster(EnemyHealth attacker) => new DamageSource(attacker);
    }
}
