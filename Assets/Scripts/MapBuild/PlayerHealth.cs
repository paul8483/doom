using System;
using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Player-side health/armor component. Wraps the pure HealthModel, exposes the
    /// TakeDamage entry point (future enemies/weapons call it too), and raises Died
    /// exactly once when health reaches zero.
    public sealed class PlayerHealth : MonoBehaviour
    {
        readonly HealthModel model = new HealthModel();
        bool deadAnnounced;

        public HealthModel Model => model;
        public int Health => model.Health;
        public int Armor => model.Armor;
        public ArmorKind ArmorType => model.ArmorType;
        public bool IsDead => model.IsDead;

        /// Raised once when the player dies (health hits 0).
        public event Action Died;

        /// Raised on non-lethal damage that actually reduced HP.
        /// Args: HP lost, optional attacker side for face turn.
        public event Action<int, FaceAttackerSide> Damaged;

        public void TakeDamage(int amount) => TakeDamage(amount, FaceAttackerSide.None);

        public void TakeDamage(int amount, FaceAttackerSide attackerSide)
        {
            if (deadAnnounced || amount <= 0) return;
            int before = model.Health;
            model.ApplyDamage(amount);
            if (model.IsDead)
            {
                deadAnnounced = true;
                Died?.Invoke();
                return;
            }
            int lost = before - model.Health;
            if (lost > 0)
                Damaged?.Invoke(lost, attackerSide);
        }

        public bool GiveHealth(int amount, int cap) => model.GiveHealth(amount, cap);
        public bool GiveArmor(ArmorKind kind) => model.GiveArmor(kind);
        public bool GiveArmorBonus(int amount) => model.GiveArmorBonus(amount);

        /// Respawn: restore full health and re-arm the Died event.
        public void ResetHealth()
        {
            model.Reset();
            deadAnnounced = false;
        }
    }
}
