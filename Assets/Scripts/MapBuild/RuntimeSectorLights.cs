using System;
using UnityEngine;
using Doom.Game;
using Doom.Map;
using Doom.Specials;
using Doom.MapBuild.Rendering;

namespace Doom.MapBuild
{
    /// Mutable per-sector light levels + thinkers. Independent of Classic/Enhanced;
    /// Enhanced presentation reads via MaterialPropertyBlock.
    ///
    /// Binding is per-sector and dirty-tracked: a glow / flicker sector changes
    /// its level every tic, and the old "any change → rebind the whole map"
    /// pass walked every renderer of every sector 35 times a second (with a
    /// GetComponentsInChildren allocation per sector, in Classic too). Now a
    /// tic touches only the sectors whose level moved, through cached renderer
    /// lists; the lamp-flicker eligibility (profile-dependent, level-independent)
    /// is applied on profile changes and wall rebuilds only.
    public sealed class RuntimeSectorLights : MonoBehaviour
    {
        public const string SectorAmbientProperty = "_SectorAmbient";
        public const string SectorAmbientWeightProperty = "_SectorAmbientWeight";
        public const string LampFlickerParamsProperty = "_LampFlickerParams";
        public const string LampFlickerLumaProperty = "_LampFlickerLuma";

        // Packed Vector4: x=enable, y=grid/8, z=amp (~30% dim), w=speed/8.
        // grid=2: independent phase per fixture cell (TLITE 2×2, FLAT2 strips, etc.).
        // MUST stay a Vector set via SetVector: SetColor sRGB→linear-converts rgb in
        // Linear projects, crushing grid/amp until the flicker is invisible.
        public static readonly Vector4 DefaultLampFlickerParams = new Vector4(
            1f, 2f / 8f, 0.30f, 2.8f / 8f);
        public const float DefaultLampFlickerLuma = 0.32f;

        MapData map;
        SectorLightState[] states;
        SectorGeometry geometry;
        WorldRenderContext renderContext;
        GraphicsModeController gfx;
        DoomRandom rng;
        Func<int> nextRandom;
        float ticAccum;
        MaterialPropertyBlock mpb;

        // Dirty tracking + per-sector renderer cache (refreshed on rebuild).
        bool[] sectorDirty;
        bool anyDirty;
        bool allDirty = true;
        MeshRenderer[][] sectorRenderers;
        bool lastEnhanced;

        public int SectorCount => states?.Length ?? 0;

        public void Init(
            MapData mapData,
            SectorGeometry sectorGeometry,
            WorldRenderContext context,
            DoomRandom random = null)
        {
            map = mapData ?? throw new ArgumentNullException(nameof(mapData));
            geometry = sectorGeometry;
            renderContext = context;
            rng = random ?? new DoomRandom();
            nextRandom = () => rng.Next();
            mpb = new MaterialPropertyBlock();
            gfx = GraphicsModeController.Ensure();

            states = new SectorLightState[map.Sectors.Length];
            sectorDirty = new bool[map.Sectors.Length];
            sectorRenderers = new MeshRenderer[map.Sectors.Length][];
            for (int s = 0; s < map.Sectors.Length; s++)
            {
                int light = map.Sectors[s].LightLevel;
                int special = map.Sectors[s].Special;
                int neighbor = RuntimeLightRules.LowestNeighborLight(
                    map, s, i => map.Sectors[i].LightLevel);
                states[s] = RuntimeLightRules.InitFromSector(light, special, neighbor);
            }

            MarkAllDirty();
            ApplyVisualsIfNeeded(force: true);
        }

        public int GetLight(int sector)
        {
            if (states == null || sector < 0 || sector >= states.Length) return 0;
            return states[sector].Light;
        }

        public SectorLightState GetState(int sector) => states[sector];

        public void SetLight(int sector, int light, bool clearThinker = true)
        {
            if (states == null || sector < 0 || sector >= states.Length) return;
            light = SectorLightState.ClampLight(light);
            if (clearThinker)
                states[sector] = SectorLightState.Static(light);
            else
            {
                var st = states[sector];
                st.Light = light;
                states[sector] = st;
            }
            MarkDirty(sector);
        }

        public void SetState(int sector, SectorLightState state)
        {
            if (states == null || sector < 0 || sector >= states.Length) return;
            state.Light = SectorLightState.ClampLight(state.Light);
            states[sector] = state;
            MarkDirty(sector);
        }

        public void RestoreFromSnapshot(int sector, int lightLevel, int lightCount)
        {
            if (states == null || sector < 0 || sector >= states.Length) return;
            int special = map.Sectors[sector].Special;
            int neighbor = RuntimeLightRules.LowestNeighborLight(map, sector, GetLight);
            var state = RuntimeLightRules.InitFromSector(
                map.Sectors[sector].LightLevel, special, neighbor);
            // Authoritative saved light + phase (count); keep kind/min/max from special.
            state.Light = SectorLightState.ClampLight(lightLevel);
            if (state.Kind != SectorLightKind.None && lightCount > 0)
                state.Count = lightCount;
            else if (state.Kind == SectorLightKind.None)
                state = SectorLightState.Static(lightLevel);
            else
                state.Light = SectorLightState.ClampLight(lightLevel);
            states[sector] = state;
            MarkDirty(sector);
        }

        /// Linedef light special application for tagged / manual targets.
        public void ApplyLinedef(int special, System.Collections.Generic.IEnumerable<int> sectors)
        {
            if (!RuntimeLightRules.TryLinedefAction(special, out int bright, out bool strobe))
                return;

            foreach (int s in sectors)
            {
                if (s < 0 || s >= states.Length) continue;
                if (strobe)
                {
                    int neighbor = RuntimeLightRules.LowestNeighborLight(map, s, GetLight);
                    states[s] = RuntimeLightRules.StartStrobe(GetLight(s), neighbor);
                }
                else if (bright == -1)
                {
                    int target = RuntimeLightRules.HighestNeighborLight(map, s, GetLight);
                    states[s] = SectorLightState.Static(target);
                }
                else if (bright == -2)
                {
                    // EV_TurnTagLightsOff: min over the sector's OWN level and
                    // its neighbours (a sector already darker than all of them
                    // stays put).
                    int target = Math.Min(
                        GetLight(s), RuntimeLightRules.LowestNeighborLight(map, s, GetLight));
                    states[s] = SectorLightState.Static(target);
                }
                else
                {
                    states[s] = SectorLightState.Static(bright);
                }
                MarkDirty(s);
            }

            ApplyVisualsIfNeeded(force: false);
        }

        void Update()
        {
            if (states == null) return;
            // Pause freezes lights with timescale 0.
            if (Time.timeScale <= 0f) return;

            ticAccum += Time.deltaTime * 35f;
            int steps = 0;
            while (ticAccum >= 1f && steps < 8)
            {
                ticAccum -= 1f;
                steps++;
                TickAll();
            }

            ApplyVisualsIfNeeded(force: false);
        }

        void TickAll()
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].Kind == SectorLightKind.None) continue;
                int before = states[i].Light;
                states[i] = RuntimeLightRules.Tick(states[i], nextRandom);
                if (states[i].Light != before) MarkDirty(i);
            }
        }

        public void NotifyProfileChanged()
        {
            MarkAllDirty();
            ApplyVisualsIfNeeded(force: true);
        }

        /// Re-bind Enhanced ambient after wall mesh rebuild for one sector.
        public void RefreshSectorVisual(int sector)
        {
            if (states == null || sector < 0 || sector >= states.Length) return;
            if (geometry == null) return;
            sectorRenderers[sector] = null; // wall renderers were recreated
            BindSector(sector, IsEnhancedAmbient(), withLampFlicker: true);
            sectorDirty[sector] = false;
        }

        bool IsEnhancedAmbient() =>
            gfx != null
            && gfx.ActiveProfile.Mode == GraphicsMode.Enhanced
            && gfx.ActiveProfile.SectorAmbientBinding;

        void MarkDirty(int sector)
        {
            sectorDirty[sector] = true;
            anyDirty = true;
        }

        void MarkAllDirty()
        {
            allDirty = true;
            anyDirty = true;
        }

        MeshRenderer[] RenderersOf(int sector)
        {
            var cached = sectorRenderers[sector];
            if (cached != null) return cached;
            var root = geometry.GetSectorRoot(sector);
            cached = root != null
                ? root.GetComponentsInChildren<MeshRenderer>(true)
                : Array.Empty<MeshRenderer>();
            sectorRenderers[sector] = cached;
            return cached;
        }

        void ApplyAmbientBlock(MeshRenderer renderer, float level)
        {
            renderer.GetPropertyBlock(mpb);
            mpb.SetColor(SectorAmbientProperty, new Color(level, level, level, 1f));
            mpb.SetFloat(SectorAmbientWeightProperty, 1f);
            renderer.SetPropertyBlock(mpb);
        }

        void ApplyLampFlickerBlock(MeshRenderer ceiling, bool enable)
        {
            ceiling.GetPropertyBlock(mpb);
            if (enable)
            {
                mpb.SetVector(LampFlickerParamsProperty, DefaultLampFlickerParams);
                mpb.SetFloat(LampFlickerLumaProperty, DefaultLampFlickerLuma);
            }
            else
            {
                mpb.SetVector(LampFlickerParamsProperty, Vector4.zero);
                mpb.SetFloat(LampFlickerLumaProperty, DefaultLampFlickerLuma);
            }
            ceiling.SetPropertyBlock(mpb);
        }

        void ApplyLampFlickerForSector(int sector, MeshRenderer[] renderers)
        {
            int special = map.Sectors[sector].Special;
            string ceilingFlat = map.Sectors[sector].CeilingFlat;
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (renderer == null) continue;

                bool enable;
                if (renderer.gameObject.name == "Ceiling")
                {
                    // Prefer WAD ceiling flat — more reliable than runtime texture names.
                    enable = EnhancedLampGlowRules.IsEligible(ceilingFlat, special);
                }
                else
                {
                    enable = EnhancedLampGlowRules.IsEligible(
                        ResolveSurfaceName(renderer), special);
                }

                ApplyLampFlickerBlock(renderer, enable);
            }
        }

        static string ResolveSurfaceName(MeshRenderer renderer)
        {
            // Wall_{index}_{TEXTURE} — texture may contain underscores.
            string goName = renderer.gameObject.name;
            if (goName.StartsWith("Wall_", StringComparison.Ordinal))
            {
                int first = goName.IndexOf('_');
                int second = first >= 0 ? goName.IndexOf('_', first + 1) : -1;
                if (second > 0 && second + 1 < goName.Length)
                    return goName.Substring(second + 1);
            }

            if (renderer.sharedMaterial == null) return null;
            var tex = renderer.sharedMaterial.mainTexture;
            return tex != null ? tex.name : null;
        }

        static MeshRenderer FindCeilingRenderer(Transform root)
        {
            var ceiling = root.Find("Ceiling");
            return ceiling != null ? ceiling.GetComponent<MeshRenderer>() : null;
        }

        /// Test helper: lamp-flicker enable (packed Color.r) on a sector's Ceiling MPB.
        public float GetCeilingLampFlicker(int sector)
        {
            if (geometry == null || map == null || sector < 0 || sector >= map.Sectors.Length)
                return 0f;
            var root = geometry.GetSectorRoot(sector);
            if (root == null) return 0f;
            var ceiling = FindCeilingRenderer(root);
            if (ceiling == null) return 0f;
            ceiling.GetPropertyBlock(mpb);
            return mpb.GetVector(LampFlickerParamsProperty).x;
        }

        /// Test helper: any light-surface renderer under the sector with flicker enabled.
        public bool SectorHasLampFlicker(int sector)
        {
            if (geometry == null || map == null || sector < 0 || sector >= map.Sectors.Length)
                return false;
            var root = geometry.GetSectorRoot(sector);
            if (root == null) return false;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(mpb);
                if (mpb.GetVector(LampFlickerParamsProperty).x > 0.5f)
                    return true;
            }
            return false;
        }

        /// Bind one sector: ambient level always (Enhanced) or clear blocks
        /// (Classic); lamp flicker only when asked (profile change / rebuild).
        void BindSector(int sector, bool enhanced, bool withLampFlicker)
        {
            var renderers = RenderersOf(sector);
            if (renderers.Length == 0) return;
            float level = states[sector].Light / 255f;
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (renderer == null) continue;
                if (!enhanced)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }
                // Merge ambient into the existing block — Clear() would drop
                // AnimatedSurfaceSystem / WallScrollController _MainTex overrides.
                ApplyAmbientBlock(renderer, level);
            }
            if (enhanced && withLampFlicker)
                ApplyLampFlickerForSector(sector, renderers);
        }

        void ApplyVisualsIfNeeded(bool force)
        {
            if (geometry == null || states == null) return;
            bool enhanced = IsEnhancedAmbient();
            bool profileFlip = enhanced != lastEnhanced;
            if (!force && !anyDirty && !profileFlip) return;

            bool everything = force || allDirty || profileFlip;
            if (everything)
            {
                // Full pass: ambient + lamp flicker for every sector (in Classic
                // this clears every block once, then nothing runs per tic).
                for (int s = 0; s < states.Length; s++)
                {
                    BindSector(s, enhanced, withLampFlicker: true);
                    sectorDirty[s] = false;
                }
            }
            else if (enhanced)
            {
                for (int s = 0; s < states.Length; s++)
                {
                    if (!sectorDirty[s]) continue;
                    BindSector(s, enhanced: true, withLampFlicker: false);
                    sectorDirty[s] = false;
                }
            }
            else
            {
                // Classic: blocks are already clear; a level change has no
                // presentation to refresh.
                Array.Clear(sectorDirty, 0, sectorDirty.Length);
            }

            anyDirty = false;
            allDirty = false;
            lastEnhanced = enhanced;
        }
    }
}
