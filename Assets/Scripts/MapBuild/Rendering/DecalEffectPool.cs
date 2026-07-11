using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild.Rendering
{
    /// Fixed-capacity world-quad decals (no URP DecalProjector). Presentation only.
    public sealed class DecalEffectPool : MonoBehaviour
    {
        public const int Capacity = 32;
        const float LifetimeSeconds = 3f;

        public static DecalEffectPool Instance { get; private set; }

        struct Slot
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public MeshFilter Filter;
            public float ExpiresAt;
            public bool Alive;
        }

        readonly Slot[] slots = new Slot[Capacity];
        Material sharedMat;
        Texture2D whiteTex;
        Mesh sharedQuad;
        bool enabledForProfile;
        int nextVictim;
        int activeCount;
        WorldRenderContext context;
        SpriteCache spriteCache;

        public int ActiveCount => activeCount;
        public int PoolCapacity => Capacity;
        public bool IsProfileEnabled => enabledForProfile;

        public void Init(WorldRenderContext renderContext, SpriteCache cache = null)
        {
            context = renderContext;
            spriteCache = cache;
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
                    pixels[i] = new Color32(255, 255, 255, 220);
                whiteTex.SetPixels32(pixels);
                whiteTex.Apply(false, true);
                whiteTex.name = "DoomDecalWhite";
                whiteTex.filterMode = FilterMode.Bilinear;
                context?.RegisterOwned(whiteTex);
            }

            if (sharedMat == null)
            {
                var factory = context?.Materials ?? new DoomMaterialFactory();
                sharedMat = factory.CreateMaterial(whiteTex, masked: true);
                context?.RegisterOwned(sharedMat);
            }

            if (sharedQuad == null)
            {
                sharedQuad = new Mesh { name = "DecalQuad" };
                sharedQuad.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                };
                sharedQuad.uv = new[]
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(1f, 1f), new Vector2(0f, 1f),
                };
                sharedQuad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                sharedQuad.RecalculateNormals();
                sharedQuad.RecalculateBounds();
                context?.RegisterOwned(sharedQuad);
            }

            for (int i = 0; i < Capacity; i++)
            {
                if (slots[i].Transform != null) continue;
                var go = new GameObject($"DecalSlot_{i}");
                go.transform.SetParent(transform, false);
                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = sharedQuad;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = sharedMat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);
                slots[i] = new Slot
                {
                    Transform = go.transform,
                    Renderer = renderer,
                    Filter = filter,
                    Alive = false,
                };
            }
        }

        public void ApplyProfile(GraphicsProfile profile)
        {
            enabledForProfile = profile.Mode == GraphicsMode.Enhanced && profile.Decals;
            if (!enabledForProfile)
                DisableAll();
        }

        public void Spawn(EffectKind kind, Vector3 pos, Vector3 normal,
                          Texture2D optionalTex = null, float worldScale = 1f / 32f)
        {
            if (!enabledForProfile) return;
            Warm();

            Vector3 n = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.forward;
            bool onFloor = Vector3.Dot(n, Vector3.up) > 0.95f;

            int idx = Acquire();
            var slot = slots[idx];
            var t = slot.Transform;
            t.gameObject.SetActive(true);
            t.position = pos + n * 0.01f;
            t.rotation = Quaternion.LookRotation(-n, Vector3.up);

            // ~0.15 m at default worldScale (1/32): 5 DOOM units.
            float size = 5f * Mathf.Max(worldScale, 1e-4f);
            if (onFloor) size *= 0.65f;
            if (kind == EffectKind.Explosion) size *= 1.5f;
            t.localScale = new Vector3(size, size, size);

            Texture2D tex = optionalTex;
            if (tex == null && spriteCache != null)
            {
                string hint = EnhancedEffectCatalog.TextureHint(kind);
                if (hint != null)
                {
                    var sm = spriteCache.Get(hint, 0, 0);
                    if (sm.IsValid && sm.Material != null)
                        tex = sm.Material.mainTexture as Texture2D;
                }
            }

            var mpb = new MaterialPropertyBlock();
            slot.Renderer.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", EnhancedEffectCatalog.ColorFor(kind));
            if (slot.Renderer.sharedMaterial != null &&
                slot.Renderer.sharedMaterial.HasProperty("_BaseColor"))
                mpb.SetColor("_BaseColor", EnhancedEffectCatalog.ColorFor(kind));
            if (tex != null)
            {
                mpb.SetTexture("_MainTex", tex);
                if (slot.Renderer.sharedMaterial != null &&
                    slot.Renderer.sharedMaterial.HasProperty("_BaseMap"))
                    mpb.SetTexture("_BaseMap", tex);
            }
            slot.Renderer.SetPropertyBlock(mpb);

            slot.Alive = true;
            slot.ExpiresAt = Time.time + LifetimeSeconds;
            slots[idx] = slot;
            Recount();
        }

        int Acquire()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!slots[i].Alive)
                    return i;
            }

            int victim = nextVictim % Capacity;
            nextVictim++;
            ForceRelease(victim);
            return victim;
        }

        void ForceRelease(int i)
        {
            var slot = slots[i];
            if (slot.Transform != null)
                slot.Transform.gameObject.SetActive(false);
            slot.Alive = false;
            slot.ExpiresAt = 0f;
            slots[i] = slot;
        }

        void DisableAll()
        {
            for (int i = 0; i < Capacity; i++)
                ForceRelease(i);
            activeCount = 0;
        }

        void Recount()
        {
            int n = 0;
            for (int i = 0; i < Capacity; i++)
                if (slots[i].Alive) n++;
            activeCount = n;
        }

        void Update()
        {
            if (!enabledForProfile)
            {
                if (activeCount > 0) DisableAll();
                return;
            }

            float now = Time.time;
            bool changed = false;
            for (int i = 0; i < Capacity; i++)
            {
                if (!slots[i].Alive) continue;
                if (now < slots[i].ExpiresAt) continue;
                ForceRelease(i);
                changed = true;
            }
            if (changed) Recount();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            for (int i = 0; i < Capacity; i++)
            {
                if (slots[i].Transform != null)
                    Destroy(slots[i].Transform.gameObject);
                slots[i] = default;
            }
        }
    }
}
