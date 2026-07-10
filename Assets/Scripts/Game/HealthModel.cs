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

        /// Authoritative restore for carry-over / save (Task 7). Clamps to model limits.
        public void Restore(int health, int armor, ArmorKind armorType)
        {
            if (health < 0) health = 0;
            if (armor < 0) armor = 0;
            if (armor > MaxArmor) armor = MaxArmor;
            if (armorType == ArmorKind.None) armor = 0;
            else if (armor == 0) armorType = ArmorKind.None;

            Health = health;
            Armor = armor;
            ArmorType = armorType;
        }

        /// Capture authoritative fields for save round-trip tests / DTO builders.
        public void Capture(out int health, out int armor, out ArmorKind armorType)
        {
            health = Health;
            armor = Armor;
            armorType = ArmorType;
        }

        /// P_GiveBody: add up to cap; false if already at cap.
        public bool GiveHealth(int amount, int cap)
        {
            if (amount <= 0 || Health >= cap) return false;
            Health = System.Math.Min(Health + amount, cap);
            return true;
        }

        /// P_GiveArmor: hits = kind rank * 100; false if Armor >= hits.
        public bool GiveArmor(ArmorKind kind)
        {
            if (kind == ArmorKind.None) return false;
            int hits = kind == ArmorKind.Green ? 100 : 200;
            if (Armor >= hits) return false;
            ArmorType = kind;
            Armor = hits;
            return true;
        }

        /// Armor bonus +N ≤ MaxArmor; if no type yet → Green.
        public bool GiveArmorBonus(int amount)
        {
            if (amount <= 0 || Armor >= MaxArmor) return false;
            if (ArmorType == ArmorKind.None) ArmorType = ArmorKind.Green;
            Armor = System.Math.Min(Armor + amount, MaxArmor);
            return true;
        }
    }
}
