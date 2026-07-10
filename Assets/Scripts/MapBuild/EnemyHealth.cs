using UnityEngine;

namespace Doom.MapBuild
{
    /// Enemy HP (Stage 6c). With MonsterController (Stage 6d): damage delegates to
    /// the brain for pain/death animation; infighting retargets the attacker.
    public sealed class EnemyHealth : MonoBehaviour
    {
        int hp;
        int corpseFrame;
        int mapThingIndex = -1;
        SpriteBillboard billboard;   // may be null in synthetic tests
        CapsuleCollider capsule;
        MonsterController controller;

        public bool IsDead => hp <= 0;
        public int Health => hp;
        public int MapThingIndex => mapThingIndex;

        public void Init(int health, int corpseFrame,
                         SpriteBillboard billboard, CapsuleCollider capsule)
        {
            hp = health;
            this.corpseFrame = corpseFrame;
            this.billboard = billboard;
            this.capsule = capsule;
        }

        public void SetMapThingIndex(int index) => mapThingIndex = index;

        public void SetController(MonsterController c) => controller = c;

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
                hp = 0;
                if (mapThingIndex >= 0)
                    LevelStatsTracker.Instance?.RegisterKill(mapThingIndex);
                if (controller != null) controller.NotifyKilled();
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
