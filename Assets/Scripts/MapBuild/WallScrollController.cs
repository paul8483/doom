using Doom.Specials;
using UnityEngine;

namespace Doom.MapBuild
{
    /// Applies constant linedef wall scrolling through a per-renderer property block.
    /// LateUpdate composes after animated-texture frame selection without instancing
    /// shared materials or rewriting mesh UVs.
    public sealed class WallScrollController : MonoBehaviour
    {
        static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

        MeshRenderer target;
        MaterialPropertyBlock block;
        int textureWidth;
        float fallbackTics;
        float offset;

        public int LineSpecial { get; private set; }
        public float CurrentOffset => offset;

        public void Configure(MeshRenderer renderer, int lineSpecial)
        {
            target = renderer;
            LineSpecial = lineSpecial;
            block ??= new MaterialPropertyBlock();
            offset = 0f;
            fallbackTics = 0f;

            textureWidth = 0;
            if (target != null && target.sharedMaterial != null &&
                target.sharedMaterial.mainTexture != null)
                textureWidth = target.sharedMaterial.mainTexture.width;
            enabled = target != null && textureWidth > 0 &&
                      WallScrollRules.TryGetUnitsPerTic(lineSpecial, out _);
            if (!enabled)
                RestoreOffset();
        }

        void LateUpdate()
        {
            if (target == null) return;
            int gameTic;
            if (LevelStatsTracker.Instance != null)
                gameTic = LevelStatsTracker.Instance.Stats.Tics;
            else
            {
                fallbackTics += Time.deltaTime * WallScrollRules.TicsPerSecond;
                gameTic = Mathf.FloorToInt(fallbackTics);
            }
            ApplyTic(gameTic);
        }

        void ApplyTic(int gameTic)
        {
            offset = WallScrollRules.NormalizedOffset(LineSpecial, textureWidth, gameTic);
            target.GetPropertyBlock(block);
            block.SetVector(MainTexStId, new Vector4(1f, 1f, offset, 0f));
            target.SetPropertyBlock(block);
        }

        public void ApplyTicForTest(int gameTic) => ApplyTic(gameTic);

        void OnDisable() => RestoreOffset();

        void RestoreOffset()
        {
            if (target == null) return;
            // Drop the block entirely when scroll is off so pooled walls reused
            // for non-scroll textures (door tracks) do not keep a stale MPB.
            target.SetPropertyBlock(null);
        }
    }
}
