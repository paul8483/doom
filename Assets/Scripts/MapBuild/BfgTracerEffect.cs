using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Visual-only BFE2 tracer hit spark. Not saved; not authoritative.
    public sealed class BfgTracerEffect : MonoBehaviour
    {
        SpriteBillboard billboard;
        int frameIndex;
        float frameLeft;

        public static void Spawn(SpriteCache cache, float worldScale, Vector3 at)
        {
            if (cache == null) return;
            var go = new GameObject("BFE2", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = at;
            var bb = go.AddComponent<SpriteBillboard>();
            bb.Init(cache, BfgRules.TracerSprite, BfgRules.TracerFrames[0], worldScale,
                doomAngleDeg: 0f, spawnCeiling: false, ceilingY: 0f);
            bb.SetStaticFrame(BfgRules.TracerFrames[0]);

            var fx = go.AddComponent<BfgTracerEffect>();
            fx.billboard = bb;
            fx.frameIndex = 0;
            fx.frameLeft = BfgRules.TracerTics[0] / 35f;
        }

        void Update()
        {
            frameLeft -= Time.deltaTime;
            if (frameLeft > 0f) return;
            frameIndex++;
            if (frameIndex >= BfgRules.TracerFrames.Length)
            {
                Destroy(gameObject);
                return;
            }
            billboard.SetStaticFrame(BfgRules.TracerFrames[frameIndex]);
            frameLeft = BfgRules.TracerTics[frameIndex] / 35f;
        }
    }
}
