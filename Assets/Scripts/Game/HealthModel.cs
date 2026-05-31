namespace Doom.Game
{
    /// Which armor the player wears. Green (security) absorbs 1/3 of damage,
    /// Blue (combat) absorbs 1/2 — ported from DOOM P_DamageMobj.
    public enum ArmorKind { None, Green, Blue }

    /// Pure player health/armor state. Engine-free so it unit-tests without Unity.
    /// Future enemy/weapon damage routes through ApplyDamage too.
    public sealed class HealthModel
    {
        public const int MaxHealth = 100;
        public const int MaxArmor = 200;

        public int Health { get; private set; }
        public int Armor { get; private set; }
        public ArmorKind ArmorType { get; private set; }

        public HealthModel() => Reset();

        public HealthModel(int health, int armor, ArmorKind armorType)
        {
            Health = health;
            Armor = armor;
            ArmorType = armorType;
        }

        public bool IsDead => Health <= 0;

        /// Apply incoming damage: armor absorbs a fraction (integer math), depleting
        /// 1 point per absorbed point; the remainder hits health (clamped at 0).
        public void ApplyDamage(int damage)
        {
            if (damage <= 0) return;
            if (ArmorType != ArmorKind.None && Armor > 0)
            {
                int saved = ArmorType == ArmorKind.Green ? damage / 3 : damage / 2;
                if (Armor <= saved) { saved = Armor; ArmorType = ArmorKind.None; }
                Armor -= saved;
                damage -= saved;
            }
            Health -= damage;
            if (Health < 0) Health = 0;
        }

        /// Restore to a fresh-spawn state (respawn).
        public void Reset()
        {
            Health = MaxHealth;
            Armor = 0;
            ArmorType = ArmorKind.None;
        }
    }
}
