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
    public sealed class RuntimeSectorLights : MonoBehaviour
    {
        public const string SectorAmbientProperty = "_SectorAmbient";
        public const string SectorAmbientWeightProperty = "_SectorAmbientWeight";
        public const string LampFlickerParamsProperty = "_LampFlickerParams";
        public const string LampFlickerLumaProperty = "_LampFlickerLuma";

        // Packed Color: r=enable, g=grid/8, b=amp (~30% dim), a=speed/8.
        // grid=2: independent phase per fixture cell (TLITE 2×2, FLAT2 strips, etc.).
        public static readonly Color DefaultLampFlickerParams = new Color(
            1f, 2f / 8f, 0.30f, 2.8f / 8f);
        public const float DefaultLampFlickerLuma = 0.32f;

        MapData map;
        SectorLightState[] states;
        SectorGeometry geometry;
        WorldRenderContext renderContext;
        GraphicsModeController gfx;
        DoomRandom rng;
        float ticAccum;
        MaterialPropertyBlock mpb;
        bool visualsDirty = true;

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
            mpb = new MaterialPropertyBlock();
            gfx = GraphicsModeController.Ensure();

            states = new SectorLightState[map.Sectors.Length];
            for (int s = 0; s < map.Sectors.Length; s++)
            {
                int light = map.Sectors[s].LightLevel;
                int special = map.Sectors[s].Special;
                int neighbor = RuntimeLightRules.LowestNeighborLight(
                    map, s, i => map.Sectors[i].LightLevel);
                states[s] = RuntimeLightRules.InitFromSector(light, special, neighbor);
            }

            visualsDirty = true;
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
            visualsDirty = true;
        }

        public void SetState(int sector, SectorLightState state)
        {
            if (states == null || sector < 0 || sector >= states.Length) return;
            state.Light = SectorLightState.ClampLight(state.Light);
            states[sector] = state;
            visualsDirty = true;
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
            visualsDirty = true;
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
                    int target = RuntimeLightRules.LowestNeighborLight(map, s, GetLight);
                    states[s] = SectorLightState.Static(target);
                }
                else
                {
                    states[s] = SectorLightState.Static(bright);
                }
            }

            visualsDirty = true;
            ApplyVisualsIfNeeded(force: true);
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
            bool changed = false;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].Kind == SectorLightKind.None) continue;
                int before = states[i].Light;
                states[i] = RuntimeLightRules.Tick(states[i], () => rng.Next());
                if (states[i].Light != before) changed = true;
            }
            if (changed) visualsDirty = true;
        }

        public void NotifyProfileChanged()
        {
            visualsDirty = true;
            ApplyVisualsIfNeeded(force: true);
        }

        /// Re-bind Enhanced ambient after wall mesh rebuild for one sector.
        public void RefreshSectorVisual(int sector)
        {
            if (states == null || sector < 0 || sector >= states.Length) return;
            if (geometry == null) return;

            bool enhanced = IsEnhancedAmbient();

            var root = geometry.GetSectorRoot(sector);
            if (root == null) return;
            float level = states[sector].Light / 255f;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
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
                // AnimatedSurfaceSystem / WallScrollController _MainTex overrides
                // and leave Fluid/Enhanced sampling Unity's missing-texture checker
                // until the next animation tick (or forever on non-animated walls
                // that inherited a stale block after a lift rebuild).
                ApplyAmbientBlock(renderer, level);
            }

            if (enhanced)
                ApplyLampFlickerForSector(sector, root);
        }

        bool IsEnhancedAmbient() =>
            gfx != null
            && gfx.ActiveProfile.Mode == GraphicsMode.Enhanced
            && gfx.ActiveProfile.SectorAmbientBinding;

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
                mpb.SetColor(LampFlickerParamsProperty, DefaultLampFlickerParams);
                mpb.SetFloat(LampFlickerLumaProperty, DefaultLampFlickerLuma);
            }
            else
            {
                mpb.SetColor(LampFlickerParamsProperty, Color.clear);
                mpb.SetFloat(LampFlickerLumaProperty, DefaultLampFlickerLuma);
            }
            ceiling.SetPropertyBlock(mpb);
        }

        void ApplyLampFlickerForSector(int sector, Transform root)
        {
            int special = map.Sectors[sector].Special;
            string ceilingFlat = map.Sectors[sector].CeilingFlat;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
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
            return mpb.GetColor(LampFlickerParamsProperty).r;
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
                if (mpb.GetColor(LampFlickerParamsProperty).r > 0.5f)
                    return true;
            }
            return false;
        }

        void ApplyVisualsIfNeeded(bool force)
        {
            if (!force && !visualsDirty) return;
            visualsDirty = false;

            bool enhanced = IsEnhancedAmbient();

            if (geometry == null || states == null) return;

            for (int s = 0; s < states.Length; s++)
            {
                var root = geometry.GetSectorRoot(s);
                if (root == null) continue;
                float level = states[s].Light / 255f;
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    var renderer = renderers[r];
                    if (renderer == null) continue;
                    if (!enhanced)
                    {
                        renderer.SetPropertyBlock(null);
                        continue;
                    }

                    ApplyAmbientBlock(renderer, level);
                }

                if (enhanced)
                    ApplyLampFlickerForSector(s, root);
            }
        }
    }
}
