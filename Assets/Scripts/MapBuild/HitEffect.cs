using UnityEngine;
using Doom.MapBuild.Rendering;

namespace Doom.MapBuild
{
    /// Short-lived billboard effect: a frame sequence of a single sprite
    /// (PUFF A->D, BLUD C->A), then Destroy. Frames/tics come from info.c.
    public sealed class HitEffect : MonoBehaviour
    {
        SpriteBillboard billboard;
        int[] frames;
        float[] secs;
        int idx;
        float left;

        public static void SpawnPuff(SpriteCache cache, float worldScale, Vector3 pos, Vector3 normal)
        {
            // Nudge slightly off the surface toward the shooter so it doesn't sink into the wall.
            Vector3 fxPos = pos + normal * 0.05f;
            Spawn(cache, worldScale, fxPos, "PUFF",
                  new[] { 0, 1, 2, 3 }, new[] { 4, 4, 4, 4 });

            var profile = GraphicsModeController.Instance != null
                ? GraphicsModeController.Instance.ActiveProfile
                : GraphicsProfile.Classic;
            if (profile.Particles)
                ParticleEffectPool.Instance?.Pulse(EffectKind.Puff, fxPos, worldScale);
            if (profile.Decals)
            {
                Texture2D tex = null;
                if (cache != null)
                {
                    var sm = cache.Get("PUFF", 0, 0);
                    if (sm.IsValid) tex = sm.Material.mainTexture as Texture2D;
                }
                DecalEffectPool.Instance?.Spawn(EffectKind.Puff, pos, normal, tex, worldScale);
            }
        }

        public static void SpawnBlood(SpriteCache cache, float worldScale, Vector3 pos)
        {
            SpawnBlood(cache, worldScale, pos, normal: null);
        }

        public static void SpawnBlood(SpriteCache cache, float worldScale, Vector3 pos, Vector3? normal)
        {
            Spawn(cache, worldScale, pos, "BLUD",
                  new[] { 2, 1, 0 }, new[] { 8, 8, 8 });

            var profile = GraphicsModeController.Instance != null
                ? GraphicsModeController.Instance.ActiveProfile
                : GraphicsProfile.Classic;
            if (profile.Particles)
                ParticleEffectPool.Instance?.Pulse(EffectKind.Blood, pos, worldScale);
            if (profile.Decals && normal.HasValue)
            {
                Texture2D tex = null;
                if (cache != null)
                {
                    var sm = cache.Get("BLUD", 2, 0);
                    if (sm.IsValid) tex = sm.Material.mainTexture as Texture2D;
                }
                DecalEffectPool.Instance?.Spawn(EffectKind.Blood, pos, normal.Value, tex, worldScale);
            }
        }

        static void Spawn(SpriteCache cache, float worldScale, Vector3 pos,
                          string sprite, int[] frames, int[] tics)
        {
            var go = new GameObject($"FX_{sprite}", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = pos;
            var bb = go.AddComponent<SpriteBillboard>();
            bb.Init(cache, sprite, frames[0], worldScale,
                    doomAngleDeg: 0f, spawnCeiling: false, ceilingY: 0f);
            bb.SetStaticFrame(frames[0]); // no rotation selection

            var fx = go.AddComponent<HitEffect>();
            fx.billboard = bb;
            fx.frames = frames;
            fx.secs = new float[tics.Length];
            for (int i = 0; i < tics.Length; i++) fx.secs[i] = tics[i] / 35f;
            fx.idx = 0;
            fx.left = fx.secs[0];
        }

        void Update()
        {
            left -= Time.deltaTime;
            if (left > 0f) return;
            idx++;
            if (idx >= frames.Length) { Destroy(gameObject); return; }
            billboard.SetStaticFrame(frames[idx]);
            left = secs[idx];
        }
    }
}
