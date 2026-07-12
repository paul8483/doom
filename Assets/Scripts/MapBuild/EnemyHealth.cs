using UnityEngine;
using Doom.Game;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Enemy HP (Stage 6c). With MonsterController (Stage 6d): damage delegates to
    /// the brain for pain/death animation; infighting retargets the attacker.
    /// Barrels (2035) use BarrelExplosion instead of a corpse frame.
    public sealed class EnemyHealth : MonoBehaviour
    {
        int hp;
        int spawnHealth;
        int corpseFrame;
        int mapThingIndex = -1;
        SpriteBillboard billboard;   // may be null in synthetic tests
        CapsuleCollider capsule;
        MonsterController controller;
        BarrelExplosion barrel;
        bool countKill;
        bool noBlood;

        public bool IsDead => hp <= 0;
        public int Health => hp;
        public int MapThingIndex => mapThingIndex;
        public bool NoBlood => noBlood;

        public void Init(int health, int corpseFrame,
                         SpriteBillboard billboard, CapsuleCollider capsule,
                         bool countKill = true, bool noBlood = false)
        {
            hp = health;
            spawnHealth = health;
            this.corpseFrame = corpseFrame;
            this.billboard = billboard;
            this.capsule = capsule;
            this.countKill = countKill;
            this.noBlood = noBlood;
        }

        public void SetMapThingIndex(int index) => mapThingIndex = index;

        public void SetController(MonsterController c) => controller = c;

        public void SetBarrel(BarrelExplosion b)
        {
            barrel = b;
            noBlood = true;
            countKill = false;
        }

        /// Apply HP from a save without firing death/pain side effects.
        public void RestoreHealth(int value)
        {
            hp = value < 0 ? 0 : value;
            if (hp <= 0 && capsule != null)
                capsule.enabled = false;
        }

        public void TakeDamage(int damage) => TakeDamage(damage, DamageSource.Player());

        public void TakeDamage(int damage, DamageSource source)
        {
            if (IsDead) return;
            hp -= damage;
            if (hp <= 0)
            {
                bool extreme = MonsterRules.ShouldUseExtremeDeath(
                    hp, spawnHealth, controller != null && controller.SupportsExtremeDeath);
                hp = 0;
                if (countKill && mapThingIndex >= 0)
                    LevelStatsTracker.Instance?.RegisterKill(mapThingIndex);
                if (controller != null) controller.NotifyKilled(extreme);
                else if (barrel != null) barrel.Begin(source);
                else Die();
                return;
            }
            if (controller != null)
            {
                if (source.MonsterAttacker != null && source.MonsterAttacker != this)
                    controller.SetTarget(source.MonsterAttacker.transform);
                controller.NotifyDamaged();
            }
        }

        void Die()
        {
            hp = 0;
            if (billboard != null && corpseFrame >= 0)
                billboard.SetStaticFrame(corpseFrame);
            if (capsule != null) capsule.enabled = false;
        }
    }
}
