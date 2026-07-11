namespace Doom.Game
{
    /// P_RadiusAttack falloff (p_map.c): damage = maxDamage − distDoom,
    /// where distDoom is approximate XY distance minus the target's radius.
    /// Barrel A_Explode uses maxDamage = radius = 128.
    public static class RadiusDamageRules
    {
        public const int BarrelMaxDamage = 128;
        public const float BarrelRadiusDoom = 128f;

        /// Returns splash damage for a target at <paramref name="distanceDoom"/>
        /// (already minus target radius). Zero when out of range.
        public static int DamageAt(int maxDamage, float distanceDoom)
        {
            if (maxDamage <= 0) return 0;
            if (distanceDoom < 0f) distanceDoom = 0f;
            if (distanceDoom >= maxDamage) return 0;
            return maxDamage - (int)distanceDoom;
        }

        public static int BarrelDamageAt(float distanceDoom)
            => DamageAt(BarrelMaxDamage, distanceDoom);
    }
}
