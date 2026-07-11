using System.Collections.Generic;
using UnityEngine;
using Doom.Game;
using Doom.Graphics;

namespace Doom.MapBuild.Rendering
{
    /// Cycles WAD flat/wall animation frames and mild fluid UV scroll in Enhanced.
    /// Uses MaterialPropertyBlock so shared materials are not instanced per frame.
    public sealed class AnimatedSurfaceSystem : MonoBehaviour
    {
        public static AnimatedSurfaceSystem Instance { get; private set; }

        struct Tracked
        {
            public Renderer Renderer;
            public Texture2D[] Frames;
            public Texture2D Original;
            public int TicDuration;
            public bool IsFluid;
            public MaterialPropertyBlock Block;
        }

        TextureAnimationCatalog catalog;
        TextureCache textures;
        readonly List<Tracked> tracked = new List<Tracked>(128);
        bool enabledForProfile;
        float accum;
        int frameIndex;

        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

        public int TrackedCount => tracked.Count;
        public bool IsProfileEnabled => enabledForProfile;
        public TextureAnimationCatalog Catalog => catalog;

        public void Init(TextureCache textureCache, TextureAnimationCatalog animationCatalog)
        {
            Instance = this;
            textures = textureCache;
            catalog = animationCatalog;
            tracked.Clear();
            accum = 0f;
            frameIndex = 0;

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
                ApplyCurrentFrame(force: true);
        }

        void Update()
        {
            if (!enabledForProfile || tracked.Count == 0) return;

            accum += Time.deltaTime * 35f; // DOOM tics
            int step = tracked[0].TicDuration;
            if (accum < step) return;
            accum -= step;
            frameIndex++;
            ApplyCurrentFrame(force: false);
        }

        void ApplyCurrentFrame(bool force)
        {
            for (int i = 0; i < tracked.Count; i++)
            {
                var t = tracked[i];
                if (t.Renderer == null || t.Frames == null || t.Frames.Length == 0)
                    continue;

                int idx = frameIndex % t.Frames.Length;
                var tex = t.Frames[idx];
                if (tex == null) continue;

                t.Renderer.GetPropertyBlock(t.Block);
                t.Block.SetTexture(MainTexId, tex);

                if (t.IsFluid)
                {
                    float scroll = (Time.time * 0.03f) % 1f;
                    t.Block.SetVector(MainTexStId, new Vector4(1f, 1f, scroll, scroll * 0.4f));
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
                if (t.Block == null) t.Block = new MaterialPropertyBlock();
                t.Renderer.GetPropertyBlock(t.Block);
                if (t.Original != null)
                    t.Block.SetTexture(MainTexId, t.Original);
                t.Block.SetVector(MainTexStId, new Vector4(1f, 1f, 0f, 0f));
                t.Renderer.SetPropertyBlock(t.Block);
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
