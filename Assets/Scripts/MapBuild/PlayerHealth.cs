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

        public int Health => model.Health;
        public int Armor => model.Armor;
        public bool IsDead => model.IsDead;

        /// Raised once when the player dies (health hits 0).
        public event Action Died;

        public void TakeDamage(int amount)
        {
            if (deadAnnounced || amount <= 0) return;
            model.ApplyDamage(amount);
            if (model.IsDead)
            {
                deadAnnounced = true;
                Died?.Invoke();
            }
        }

        /// Respawn: restore full health and re-arm the Died event.
        public void ResetHealth()
        {
            model.Reset();
            deadAnnounced = false;
        }
    }
}
