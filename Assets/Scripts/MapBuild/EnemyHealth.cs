using UnityEngine;

namespace Doom.MapBuild
{
    /// Enemy HP (Stage 6c). No AI and no pain state: accumulates damage, at zero
    /// switches to the corpse frame, disables the collider, and ignores further damage.
    public sealed class EnemyHealth : MonoBehaviour
    {
        int hp;
        int corpseFrame;
        SpriteBillboard billboard;   // may be null in synthetic tests
        CapsuleCollider capsule;

        public bool IsDead => hp <= 0;

        public void Init(int health, int corpseFrame,
                         SpriteBillboard billboard, CapsuleCollider capsule)
        {
            hp = health;
            this.corpseFrame = corpseFrame;
            this.billboard = billboard;
            this.capsule = capsule;
        }

        public void TakeDamage(int damage)
        {
            if (IsDead) return;
            hp -= damage;
            if (hp <= 0) Die();
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
