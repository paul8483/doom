using UnityEngine;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Barrel death: BEXP animation, DSBAREXP, P_RadiusAttack splash, then destroy.
    /// Wired by ThingSpawner for doomednum 2035; EnemyHealth calls Begin on lethal hit.
    public sealed class BarrelExplosion : MonoBehaviour
    {
        SpriteBillboard billboard;
        CapsuleCollider capsule;
        SpriteCache cache;
        float worldScale;
        SoundSystem sound;
        bool started;
        int idx;
        float left;

        public void Init(SpriteBillboard billboard, CapsuleCollider capsule,
                         SpriteCache cache, float worldScale, SoundSystem sound)
        {
            this.billboard = billboard;
            this.capsule = capsule;
            this.cache = cache;
            this.worldScale = worldScale;
            this.sound = sound;
            enabled = false;
        }

        /// Start explode sequence. <paramref name="source"/> is who shot the barrel
        /// (vanilla mo->target) for splash attribution / infighting.
        public void Begin(DamageSource source)
        {
            if (started) return;
            started = true;

            if (capsule != null) capsule.enabled = false;

            var id = GetComponent<MapThingIdentity>();
            if (id != null)
                WorldStateRegistry.Instance?.UnregisterMapThing(id.MapThingIndex);

            if (billboard != null)
                billboard.SetSprite(BarrelRules.ExplodeSprite, BarrelRules.ExplodeFrames[0]);

            Vector3 pos = transform.position;
            sound?.PlayAt(BarrelRules.ExplodeSound, pos);

            // Blast origin at mid-height (vanilla mobj z + height/2 approx).
            float midY = capsule != null ? capsule.height * 0.5f : 20f * worldScale;
            RadiusDamageExecutor.ApplyBarrelBlast(
                pos + Vector3.up * midY, worldScale, transform, source);

            idx = 0;
            left = BarrelRules.ExplodeTics[0] / 35f;
            enabled = true;
        }

        void Update()
        {
            left -= Time.deltaTime;
            if (left > 0f) return;
            idx++;
            if (idx >= BarrelRules.ExplodeFrames.Length)
            {
                Destroy(gameObject);
                return;
            }
            billboard?.SetStaticFrame(BarrelRules.ExplodeFrames[idx]);
            left = BarrelRules.ExplodeTics[idx] / 35f;
        }
    }
}
