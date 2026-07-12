using System.Collections.Generic;
using UnityEngine;
using Doom.Game;
using Doom.Graphics;

namespace Doom.MapBuild.Rendering
{
    /// Cycles WAD flat/wall animation frames and mild fluid UV scroll in Enhanced.
    /// Fluids cross-fade between frames; other animated textures hard-cut (vanilla).
    /// Uses MaterialPropertyBlock so shared materials are not instanced per frame.
    public sealed class AnimatedSurfaceSystem : MonoBehaviour
    {
        public static AnimatedSurfaceSystem Instance { get; private set; }

        struct Tracked
        {
            public Renderer Renderer;
            public Texture2D[] Frames;
            public Texture2D Original;
            public Shader OriginalShader;
            public int TicDuration;
            public bool IsFluid;
            public MaterialPropertyBlock Block;
        }

        TextureAnimationCatalog catalog;
        TextureCache textures;
        readonly List<Tracked> tracked = new List<Tracked>(128);
        bool enabledForProfile;
        float ticClock;

        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int MainTexBId = Shader.PropertyToID("_MainTexB");
        static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        static readonly int FrameBlendId = Shader.PropertyToID("_FrameBlend");

        public int TrackedCount => tracked.Count;
        public bool IsProfileEnabled => enabledForProfile;
        public TextureAnimationCatalog Catalog => catalog;

        public void Init(TextureCache textureCache, TextureAnimationCatalog animationCatalog)
        {
            Instance = this;
            textures = textureCache;
            catalog = animationCatalog;
            tracked.Clear();
            ticClock = 0f;

            if (textures == null || catalog == null) return;

            // Pre-warm every frame texture while the WAD-backed cache is still valid.
            foreach (var seq in catalog.Sequences)
            {
                for (int i = 0; i < seq.Frames.Length; i++)
                    textures.GetTexture(seq.Frames[i]);
            }

            var renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.sharedMaterial == null) continue;
                var main = r.sharedMaterial.mainTexture as Texture2D;
                if (main == null || string.IsNullOrEmpty(main.name)) continue;
                if (!catalog.TryGet(main.name, out var seq) || !seq.IsValid) continue;

                var frames = new Texture2D[seq.Frames.Length];
                for (int f = 0; f < seq.Frames.Length; f++)
                    frames[f] = textures.GetTexture(seq.Frames[f]);

                bool fluid = MaterialSurfaceClassifier.Classify(seq.BaseName, !seq.IsWall)
                             == MaterialSurfaceCategory.Fluid;

                tracked.Add(new Tracked
                {
                    Renderer = r,
                    Frames = frames,
                    Original = main,
                    OriginalShader = r.sharedMaterial.shader,
                    TicDuration = Mathf.Max(1, seq.TicDuration),
                    IsFluid = fluid,
                    Block = new MaterialPropertyBlock(),
                });
            }

            var profile = GraphicsModeController.Instance != null
                ? GraphicsModeController.Instance.ActiveProfile
                : GraphicsProfile.Classic;
            ApplyProfile(profile);
        }

        public void ApplyProfile(GraphicsProfile profile)
        {
            enabledForProfile = profile.Mode == GraphicsMode.Enhanced && profile.AnimatedFluids;
            if (!enabledForProfile)
                RestoreOriginals();
            else
            {
                PromoteFluidShaders();
                ApplyCurrentFrame();
            }
        }

        void PromoteFluidShaders()
        {
            var fluid = Shader.Find(DoomMaterialFactory.FluidName);
            if (fluid == null) return;
            for (int i = 0; i < tracked.Count; i++)
            {
                var t = tracked[i];
                if (!t.IsFluid || t.Renderer == null || t.Renderer.sharedMaterial == null)
                    continue;
                if (t.Renderer.sharedMaterial.shader != fluid)
                    t.Renderer.sharedMaterial.shader = fluid;
            }
        }

        void Update()
        {
            if (!enabledForProfile || tracked.Count == 0) return;
            ticClock += Time.deltaTime * 35f;
            ApplyCurrentFrame();
        }

        void ApplyCurrentFrame()
        {
            for (int i = 0; i < tracked.Count; i++)
            {
                var t = tracked[i];
                if (t.Renderer == null || t.Frames == null || t.Frames.Length == 0)
                    continue;

                float duration = t.TicDuration;
                // Fluids linger a bit longer so the cross-fade reads as flow, not pop.
                if (t.IsFluid) duration *= 1.35f;
                duration = Mathf.Max(1f, duration);

                float phase = ticClock / duration;
                int idx = Mathf.FloorToInt(phase);
                // Positive modulo for long-running clocks.
                idx %= t.Frames.Length;
                if (idx < 0) idx += t.Frames.Length;
                float frac = phase - Mathf.Floor(phase);

                var tex = t.Frames[idx];
                if (tex == null) continue;

                t.Renderer.GetPropertyBlock(t.Block);
                t.Block.SetTexture(MainTexId, tex);

                if (t.IsFluid && t.Frames.Length > 1)
                {
                    int next = (idx + 1) % t.Frames.Length;
                    var nextTex = t.Frames[next] != null ? t.Frames[next] : tex;
                    t.Block.SetTexture(MainTexBId, nextTex);
                    // Smoothstep removes the harsh mid-transition flicker of a linear cut.
                    t.Block.SetFloat(FrameBlendId, frac * frac * (3f - 2f * frac));

                    float scroll = (Time.time * 0.02f) % 1f;
                    t.Block.SetVector(MainTexStId, new Vector4(1f, 1f, scroll, scroll * 0.35f));
                }
                else
                {
                    t.Block.SetFloat(FrameBlendId, 0f);
                    t.Block.SetVector(MainTexStId, new Vector4(1f, 1f, 0f, 0f));
                }

                t.Renderer.SetPropertyBlock(t.Block);
                tracked[i] = t;
            }
        }

        void RestoreOriginals()
        {
            for (int i = 0; i < tracked.Count; i++)
            {
                var t = tracked[i];
                if (t.Renderer == null) continue;

                if (t.Renderer.sharedMaterial != null && t.OriginalShader != null &&
                    t.Renderer.sharedMaterial.shader != t.OriginalShader)
                    t.Renderer.sharedMaterial.shader = t.OriginalShader;

                if (t.Block == null) t.Block = new MaterialPropertyBlock();
                t.Renderer.GetPropertyBlock(t.Block);
                if (t.Original != null)
                    t.Block.SetTexture(MainTexId, t.Original);
                t.Block.SetFloat(FrameBlendId, 0f);
                t.Block.SetVector(MainTexStId, new Vector4(1f, 1f, 0f, 0f));
                // Clear block entirely so Classic shared materials show through cleanly.
                t.Renderer.SetPropertyBlock(null);
                tracked[i] = t;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RestoreOriginals();
            tracked.Clear();
        }
    }
}
