using UnityEngine;
using Doom.MapBuild.Rendering;
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

            // Enhanced 3D presentation is the intact barrel only; explode frames
            // stay on the classic billboard path.
            GetComponent<ExperimentalPickupModel>()?.RevertToBillboard();

            // Stop the idle blink and leave the pickup/redraw path: BEXP is an
            // effect and renders through the generic sprite pipeline.
            var idle = GetComponent<PickupAnimator>();
            if (idle != null) Destroy(idle);
            if (billboard != null)
            {
                billboard.SetPickupUpscale(false);
                billboard.SetSprite(BarrelRules.ExplodeSprite, BarrelRules.ExplodeFrames[0]);
            }

            Vector3 pos = transform.position;
            sound?.PlayAt(BarrelRules.ExplodeSound, pos);

            // A_Explode runs on frame D (tic 15 after death), so a chain of
            // barrels ripples instead of detonating in one frame and the player
            // who shot one has the vanilla moment to step back.
            blastSource = source;
            idx = 0;
            left = BarrelRules.ExplodeTics[0] / 35f;
            enabled = true;
            if (BarrelRules.ExplodeFrameIndex == 0) Explode();
        }

        DamageSource blastSource;
        bool exploded;

        void Explode()
        {
            if (exploded) return;
            exploded = true;
            // Blast origin at mid-height (vanilla mobj z + height/2 approx).
            float midY = capsule != null ? capsule.height * 0.5f : 20f * worldScale;
            Vector3 blastOrigin = transform.position + Vector3.up * midY;
            RadiusDamageExecutor.ApplyBarrelBlast(
                blastOrigin, worldScale, transform, blastSource);

            // Presentation only — after damage so timing/rules stay unchanged.
            EnhancedLightSystem.Instance?.PulseExplosion(blastOrigin, worldScale);
            ParticleEffectPool.Instance?.Pulse(EffectKind.Explosion, blastOrigin, worldScale);
        }

        void Update()
        {
            left -= Time.deltaTime;
            if (left > 0f) return;
            idx++;
            if (idx >= BarrelRules.ExplodeFrames.Length)
            {
                Explode(); // never lose the blast if the sequence is cut short
                Destroy(gameObject);
                return;
            }
            billboard?.SetStaticFrame(BarrelRules.ExplodeFrames[idx]);
            left = BarrelRules.ExplodeTics[idx] / 35f;
            if (idx == BarrelRules.ExplodeFrameIndex) Explode();
        }
    }
}
