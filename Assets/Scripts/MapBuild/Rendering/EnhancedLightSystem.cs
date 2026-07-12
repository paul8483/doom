using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Doom.Game;
using Doom.Things;

namespace Doom.MapBuild.Rendering
{
    /// Event-driven bounded dynamic lights for Enhanced mode. Presentation only —
    /// does not touch damage, AI, wake, or save data.
    public sealed class EnhancedLightSystem : MonoBehaviour
    {
        public static EnhancedLightSystem Instance { get; private set; }

        struct Entry
        {
            public int Handle;
            public Vector3 Position;
            public Transform Follow;
            public Vector3 FollowOffset;
            public Color Color;
            public float Intensity;
            public float Range;
            public float Importance;
            public bool WantsShadow;
            public float ExpiresAt;
            public bool Alive;
        }

        EnhancedLightPool pool;
        readonly List<Entry> entries = new List<Entry>(64);
        int nextHandle = 1;
        bool enabledForProfile;
        bool shadowsForProfile;
        Camera worldCamera;
        WorldRenderContext context;

        readonly float[] scores = new float[128];
        readonly bool[] wantsShadowBuf = new bool[128];
        readonly bool[] shadowSet = new bool[128];
        readonly int[] candToEntry = new int[128];
        readonly int[] activeSel = new int[EnhancedLightPool.MaxLights];
        readonly int[] shadowSel = new int[EnhancedLightPool.MaxShadows];
        readonly Vector3[] framePos = new Vector3[EnhancedLightPool.MaxLights];
        readonly Color[] frameColor = new Color[EnhancedLightPool.MaxLights];
        readonly float[] frameIntensity = new float[EnhancedLightPool.MaxLights];
        readonly float[] frameRange = new float[EnhancedLightPool.MaxLights];
        readonly bool[] frameShadow = new bool[EnhancedLightPool.MaxLights];

        public int RequestCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].Alive) n++;
                return n;
            }
        }

        public int ActiveLightCount => pool != null ? pool.CountEnabled() : 0;
        public int ShadowCasterCount => pool != null ? pool.CountShadows() : 0;
        public int PoolCapacity => EnhancedLightPool.MaxLights;
        public int ShadowCapacity => EnhancedLightPool.MaxShadows;
        public bool IsProfileEnabled => enabledForProfile;

        public void Init(WorldRenderContext renderContext)
        {
            context = renderContext;
            pool?.Dispose();
            pool = new EnhancedLightPool(transform);
            entries.Clear();
            nextHandle = 1;
            Instance = this;

            var profile = GraphicsModeController.Instance != null
                ? GraphicsModeController.Instance.ActiveProfile
                : GraphicsProfile.Classic;
            ApplyProfile(profile);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            pool?.Dispose();
            pool = null;
        }

        public void ApplyProfile(GraphicsProfile profile)
        {
            enabledForProfile = profile.Mode == GraphicsMode.Enhanced && profile.DynamicLights;
            shadowsForProfile = enabledForProfile && profile.Shadows;
            ApplyWorldShadowCasting(shadowsForProfile);

            if (!enabledForProfile)
                pool?.DisableAll();
        }

        void ApplyWorldShadowCasting(bool enabled)
        {
            if (context == null) return;
            var mode = enabled ? ShadowCastingMode.On : ShadowCastingMode.Off;
            var recv = enabled;
            for (int i = 0; i < context.Renderers.Count; i++)
            {
                var r = context.Renderers[i];
                if (r == null) continue;
                // World geometry only — sprite billboards stay Off (simplified policy).
                if (r.GetComponent<SpriteBillboard>() != null) continue;
                r.shadowCastingMode = mode;
                r.receiveShadows = recv;
            }
        }

        public void SetWorldCamera(Camera camera) => worldCamera = camera;

        /// Sticky decoration light. Returns handle for Release. Follows transform.
        public int RegisterSticky(
            Vector3 position,
            EnhancedEmissionDef def,
            float worldScale,
            Transform follow = null,
            Vector3 followOffset = default)
        {
            Color c = new Color(def.ColorR, def.ColorG, def.ColorB, 1f);
            return AddEntry(
                position,
                follow,
                followOffset,
                c,
                def.Intensity,
                def.RangeDoom * worldScale,
                def.Importance,
                def.WantsShadow,
                float.PositiveInfinity);
        }

        public void Release(int handle)
        {
            if (handle <= 0) return;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].Alive || entries[i].Handle != handle) continue;
                var e = entries[i];
                e.Alive = false;
                entries[i] = e;
                return;
            }
        }

        public void Pulse(
            Vector3 position,
            Color color,
            float intensity,
            float range,
            float durationSeconds,
            float importance,
            bool wantsShadow)
        {
            float expires = durationSeconds <= 0f
                ? Time.time + 0.05f
                : Time.time + durationSeconds;
            AddEntry(position, null, default, color, intensity, range, importance,
                     wantsShadow, expires);
        }

        public void PulseMuzzle(Vector3 position, float worldScale, float durationTics)
        {
            // Keep the cue readable in dark sectors without washing the near camera
            // (spawn is ~camera-forward) or overdriving HDR bloom.
            float duration = Mathf.Max(1f, durationTics) / 35f;
            Pulse(
                position,
                new Color(1f, 0.82f, 0.55f),
                intensity: 0.9f,
                range: 48f * worldScale,
                durationSeconds: duration,
                importance: 1.0f,
                wantsShadow: false);
        }

        public void PulseProjectile(Vector3 position, float worldScale, bool impact)
        {
            Pulse(
                position,
                impact ? new Color(1f, 0.45f, 0.15f) : new Color(1f, 0.55f, 0.2f),
                intensity: impact ? 2.5f : 1.4f,
                range: (impact ? 128f : 80f) * worldScale,
                durationSeconds: impact ? 0.35f : 0.12f,
                importance: impact ? 1.3f : 0.9f,
                wantsShadow: impact);
        }

        public void PulseExplosion(Vector3 position, float worldScale)
        {
            Pulse(
                position,
                new Color(1f, 0.5f, 0.15f),
                intensity: 3.5f,
                range: 192f * worldScale,
                durationSeconds: 0.55f,
                importance: 1.5f,
                wantsShadow: true);
        }

        int AddEntry(
            Vector3 position,
            Transform follow,
            Vector3 followOffset,
            Color color,
            float intensity,
            float range,
            float importance,
            bool wantsShadow,
            float expiresAt)
        {
            int handle = nextHandle++;
            // Reuse dead slots to avoid unbounded list growth after warm-up stress.
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Alive) continue;
                entries[i] = new Entry
                {
                    Handle = handle,
                    Position = position,
                    Follow = follow,
                    FollowOffset = followOffset,
                    Color = color,
                    Intensity = intensity,
                    Range = range,
                    Importance = importance,
                    WantsShadow = wantsShadow,
                    ExpiresAt = expiresAt,
                    Alive = true,
                };
                return handle;
            }

            entries.Add(new Entry
            {
                Handle = handle,
                Position = position,
                Follow = follow,
                FollowOffset = followOffset,
                Color = color,
                Intensity = intensity,
                Range = range,
                Importance = importance,
                WantsShadow = wantsShadow,
                ExpiresAt = expiresAt,
                Alive = true,
            });
            return handle;
        }

        void LateUpdate()
        {
            if (pool == null) return;

            float now = Time.time;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!e.Alive) continue;
                if (now >= e.ExpiresAt)
                {
                    e.Alive = false;
                    entries[i] = e;
                    continue;
                }

                if (e.Follow != null)
                {
                    e.Position = e.Follow.position + e.FollowOffset;
                    entries[i] = e;
                }
            }

            if (!enabledForProfile)
            {
                pool.DisableAll();
                return;
            }

            if (worldCamera == null && context != null)
                worldCamera = context.WorldCamera;
            Vector3 camPos = worldCamera != null ? worldCamera.transform.position : Vector3.zero;

            int cand = 0;
            for (int i = 0; i < entries.Count && cand < scores.Length; i++)
            {
                var e = entries[i];
                if (!e.Alive) continue;
                candToEntry[cand] = i;
                scores[cand] = EnhancedLightPool.Score(camPos, e.Position, e.Importance);
                wantsShadowBuf[cand] = e.WantsShadow && shadowsForProfile;
                shadowSet[cand] = false;
                cand++;
            }

            EnhancedLightPool.Select(
                scores, wantsShadowBuf, cand,
                activeSel, out int activeCount,
                shadowSel, out int shadowCount);

            for (int i = 0; i < shadowCount; i++)
                shadowSet[shadowSel[i]] = true;

            int frameCount = 0;
            for (int i = 0; i < activeCount && frameCount < EnhancedLightPool.MaxLights; i++)
            {
                int candIdx = activeSel[i];
                var e = entries[candToEntry[candIdx]];
                framePos[frameCount] = e.Position;
                frameColor[frameCount] = e.Color;
                frameIntensity[frameCount] = e.Intensity;
                frameRange[frameCount] = e.Range;
                frameShadow[frameCount] = shadowSet[candIdx];
                frameCount++;
            }

            pool.ApplyFrame(framePos, frameColor, frameIntensity, frameRange, frameShadow, frameCount);
        }
    }
}
