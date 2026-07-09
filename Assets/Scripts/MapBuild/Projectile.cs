using UnityEngine;
using Doom.Game;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Monster projectile (imp fireball): straight flight at DOOM speed,
    /// sphere-cast collision, explosion frames, then Destroy. Fly/explode
    /// frames come from MonsterDef; damage is rolled on impact (p_inter.c).
    public sealed class Projectile : MonoBehaviour
    {
        SpriteCache cache;
        MonsterDef def;
        float worldScale;
        Vector3 velocity;         // m/s
        DoomRandom rng;
        SpriteBillboard bb;
        float castRadius;
        EnemyHealth owner;

        int flyIdx;
        float flyLeft;
        bool exploding;
        int boomIdx;
        float boomLeft;

        public static void Launch(SpriteCache cache, MonsterDef def, float worldScale,
                                  DoomRandom rng, Vector3 from, Vector3 targetPoint,
                                  EnemyHealth owner = null)
        {
            var go = new GameObject($"Missile_{def.MissileSprite}",
                typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = from;
            var bb = go.AddComponent<SpriteBillboard>();
            bb.Init(cache, def.MissileSprite, def.MissileFlyFrames[0], worldScale,
                    doomAngleDeg: 0f, spawnCeiling: false, ceilingY: 0f);
            bb.SetStaticFrame(def.MissileFlyFrames[0]); // BAL1 без ротаций

            var p = go.AddComponent<Projectile>();
            p.cache = cache; p.def = def; p.worldScale = worldScale; p.rng = rng;
            p.bb = bb; p.owner = owner;
            float speed = def.MissileSpeed * 35f * worldScale;      // юниты/тик → м/с
            Vector3 delta = targetPoint - from;
            if (delta.sqrMagnitude < 1e-8f) delta = Vector3.forward;
            p.velocity = delta.normalized * speed;
            p.castRadius = def.MissileRadius * worldScale;
            p.flyIdx = 0;
            p.flyLeft = def.MissileFlyTics[0] / 35f;
        }

        void Update()
        {
            if (exploding) { TickExplosion(); return; }

            // Полётная анимация (2 кадра циклом).
            flyLeft -= Time.deltaTime;
            if (flyLeft <= 0f)
            {
                flyIdx = (flyIdx + 1) % def.MissileFlyFrames.Length;
                bb.SetStaticFrame(def.MissileFlyFrames[flyIdx]);
                flyLeft = def.MissileFlyTics[flyIdx] / 35f;
            }

            // Движение со сферокастом по отрезку кадра.
            Vector3 delta = velocity * Time.deltaTime;
            float dist = delta.magnitude;
            if (dist > 1e-8f && Physics.SphereCast(transform.position, castRadius, delta.normalized,
                                   out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position += delta.normalized * hit.distance;
                OnImpact(hit.collider);
                return;
            }
            transform.position += delta;
        }

        void OnImpact(Collider hitCollider)
        {
            int damage = MonsterRules.RollDamage(rng, def.MissileImpactMod, def.MissileImpactMult);

            var player = hitCollider.GetComponent<PlayerHealth>();
            var enemy = hitCollider.GetComponent<EnemyHealth>();
            if (player != null) player.TakeDamage(damage);
            else if (enemy != null && !enemy.IsDead)
                enemy.TakeDamage(damage, owner != null ? DamageSource.Monster(owner) : DamageSource.Player());

            exploding = true;
            boomIdx = 0;
            bb.SetStaticFrame(def.MissileExplodeFrames[0]);
            boomLeft = def.MissileExplodeTics[0] / 35f;
            velocity = Vector3.zero;
        }

        void TickExplosion()
        {
            boomLeft -= Time.deltaTime;
            if (boomLeft > 0f) return;
            boomIdx++;
            if (boomIdx >= def.MissileExplodeFrames.Length) { Destroy(gameObject); return; }
            bb.SetStaticFrame(def.MissileExplodeFrames[boomIdx]);
            boomLeft = def.MissileExplodeTics[boomIdx] / 35f;
        }
    }
}
