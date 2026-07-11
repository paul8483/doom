using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild.Rendering
{
    /// Fixed-capacity presentation particle bursts for Enhanced mode.
    public sealed class ParticleEffectPool : MonoBehaviour
    {
        public const int Capacity = 24;

        public static ParticleEffectPool Instance { get; private set; }

        struct Slot
        {
            public ParticleSystem System;
            public float ExpiresAt;
            public bool Alive;
        }

        readonly Slot[] slots = new Slot[Capacity];
        Texture2D whiteTex;
        bool enabledForProfile;
        int nextVictim;
        WorldRenderContext context;

        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i].Alive) n++;
                return n;
            }
        }

        public int PoolCapacity => Capacity;
        public bool IsProfileEnabled => enabledForProfile;

        public void Init(WorldRenderContext renderContext)
        {
            context = renderContext;
            Instance = this;
            Warm();

            var profile = GraphicsModeController.Instance != null
                ? GraphicsModeController.Instance.ActiveProfile
                : GraphicsProfile.Classic;
            ApplyProfile(profile);
        }

        void Warm()
        {
            if (whiteTex == null)
            {
                whiteTex = new Texture2D(4, 4, TextureFormat.RGBA32, false, false);
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(255, 255, 255, 255);
                whiteTex.SetPixels32(pixels);
                whiteTex.Apply(false, true);
                whiteTex.name = "DoomParticleWhite";
                whiteTex.filterMode = FilterMode.Bilinear;
                whiteTex.wrapMode = TextureWrapMode.Clamp;
                context?.RegisterOwned(whiteTex);
            }

            for (int i = 0; i < Capacity; i++)
            {
                if (slots[i].System != null) continue;
                var go = new GameObject($"ParticleSlot_{i}");
                go.transform.SetParent(transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                ConfigureSystem(ps);
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = CreateParticleMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);
                slots[i] = new Slot { System = ps, Alive = false, ExpiresAt = 0f };
            }
        }

        Material CreateParticleMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find(DoomMaterialFactory.ClassicCutoutName);
            if (shader == null)
                throw new System.InvalidOperationException("No particle shader available");
            var mat = new Material(shader);
            mat.mainTexture = whiteTex;
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", whiteTex);
            context?.RegisterOwned(mat);
            return mat;
        }

        static void ConfigureSystem(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.4f;
            main.startLifetime = 0.35f;
            main.startSize = 0.08f;
            main.startSpeed = 0.6f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = grad;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        public void ApplyProfile(GraphicsProfile profile)
        {
            enabledForProfile = profile.Mode == GraphicsMode.Enhanced && profile.Particles;
            if (!enabledForProfile)
                DisableAll();
        }

        public void Pulse(EffectKind kind, Vector3 pos, float worldScale)
        {
            if (!enabledForProfile) return;
            Warm();

            int idx = Acquire();
            var slot = slots[idx];
            var ps = slot.System;
            var go = ps.gameObject;
            go.SetActive(true);
            go.transform.position = pos;

            float life = EnhancedEffectCatalog.Lifetime(kind);
            Color color = EnhancedEffectCatalog.ColorFor(kind);
            float size = Mathf.Max(0.04f, 0.12f * Mathf.Max(worldScale, 1f / 32f) * 32f);

            var main = ps.main;
            main.startLifetime = life;
            main.startColor = color;
            main.startSize = size * (kind == EffectKind.Explosion ? 1.8f : 1f);
            main.startSpeed = kind == EffectKind.Explosion ? 1.4f : 0.7f;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Emit(kind == EffectKind.Explosion ? 18 : 10);

            slot.Alive = true;
            slot.ExpiresAt = Time.time + life + 0.05f;
            slots[idx] = slot;
        }

        int Acquire()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!slots[i].Alive)
                    return i;
            }

            // Oldest reuse — round-robin victim.
            int victim = nextVictim % Capacity;
            nextVictim++;
            ForceRelease(victim);
            return victim;
        }

        void ForceRelease(int i)
        {
            var slot = slots[i];
            if (slot.System != null)
            {
                slot.System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                slot.System.gameObject.SetActive(false);
            }
            slot.Alive = false;
            slot.ExpiresAt = 0f;
            slots[i] = slot;
        }

        void DisableAll()
        {
            for (int i = 0; i < Capacity; i++)
                ForceRelease(i);
        }

        void Update()
        {
            if (!enabledForProfile)
            {
                if (ActiveCount > 0) DisableAll();
                return;
            }

            float now = Time.time;
            for (int i = 0; i < Capacity; i++)
            {
                if (!slots[i].Alive) continue;
                if (now < slots[i].ExpiresAt) continue;
                ForceRelease(i);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            for (int i = 0; i < Capacity; i++)
            {
                if (slots[i].System != null)
                    Destroy(slots[i].System.gameObject);
                slots[i] = default;
            }
        }
    }
}
