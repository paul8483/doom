using System.Collections.Generic;
using UnityEngine;
using Doom.Game;
using Doom.MapBuild.Rendering;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Experimental TRELLIS.2 presentation for allowlisted things (pickups,
    /// decorations, exploding barrels). Gameplay/collision remain on the
    /// original root; Enhanced swaps only the visible billboard for a textured
    /// 3D model from Resources.
    public sealed class ExperimentalPickupModel : MonoBehaviour
    {
        const string ResourceRoot = "ExperimentalPickups/";

        SpriteBillboard billboard;
        MeshRenderer billboardRenderer;
        GameObject modelRoot;
        Renderer[] modelRenderers;
        readonly List<Material> ownedMaterials = new List<Material>();
        bool lastUseMesh;
        bool lockedToBillboard;
        bool settingsControllerSeen;
        bool gemBlink;
        PickupAnimation blinkAnimation;
        int brightFrame;
        int lastGemBlink = -1;

        public bool HasModel => modelRoot != null;
        public bool ModelVisible => HasModel && modelRoot.activeSelf;

        public static ExperimentalPickupModel TryAttach(
            GameObject pickupRoot,
            int doomedNum,
            float worldScale,
            SpriteBillboard billboard)
        {
            if (pickupRoot == null || !TryGetResource(doomedNum, out string resource))
                return null;
            if (!ThingTable.TryGet(doomedNum, out var def))
                return null;

            var presentation = pickupRoot.AddComponent<ExperimentalPickupModel>();
            // Decoration meshes match the native sprite's visual height, not
            // the (shorter) mobjinfo collision height — TRE2's patch is 124 px
            // tall vs a 64-unit collision box, which dwarfed the 3D tree.
            float heightUnits = SpriteHeightPx(doomedNum, def.Height);
            // TRELLIS textures contain their own baked presentation. The
            // Resources-loaded shader keeps them readable in dark sectors and
            // avoids standalone stripping that affected the original Lit pass.
            bool useUnlit = true;
            // 2014 used a global 0.65 boost on the old voxel mesh; the
            // regenerated flask (2026-08-21) has real dark iron straps, so the
            // glow moved to a glass-only emission mask below.
            float emissionStrength = 0f;
            string pulseMaskResource = doomedNum switch
            {
                2012 => ResourceRoot + "MEDIA0/MEDIA0_emission",
                // Armor bonus: smooth sine pulse on white glint + yellow bar.
                2015 => ResourceRoot + "BON2A0/BON2A0_emission",
                // Green armor gem: vanilla ARM1 A/B blink (info.c, 6+6 tics).
                // Mask derived from the ARM1B0 albedo (red-dominant texels).
                2018 => ResourceRoot + "ARM1B0/ARM1B0_emission",
                // Barrel ring lamps + green band, zoned by mesh height so the
                // emblem's red stays dark; flashes on frame B (S_BAR1 6+6).
                2035 => ResourceRoot + "BAR1B0/BAR1B0_emission",
                // Floor lamp: amber cylinder + dome ports glow STEADY — the
                // lamp has no animation, so the blink path never leaves its
                // bright phase (blinkAnimation stays null).
                2028 => ResourceRoot + "COLUA0/COLUA0_emission",
                // Health bonus: only the green glass glows (steady); the star
                // glint animation stays billboard-only, so no blink animation
                // even though 2014 sits in PickupAnimationTable.
                2014 => ResourceRoot + "BON1A0/BON1A0_emission",
                _ => null,
            };
            bool gemBlink = doomedNum == 2018 || doomedNum == 2035
                || doomedNum == 2028 || doomedNum == 2014;
            // Which animation frame is the mask's BRIGHT phase: the armor gem
            // shines on A (frame 0), the barrel's lamps flash on B (frame 1).
            int blinkBrightFrame = doomedNum == 2035 ? 1 : 0;
            // BON2: slower sine than MEDIA0 so the stripe flicker reads smooth.
            float pulseStrength = doomedNum == 2015 ? 1.0f : 1.2f;
            float pulseSpeed = doomedNum == 2015 ? 4f : 8f;
            // The blink phase source: pickups sit in PickupAnimationTable, but
            // the barrel's cadence lives in BarrelRules (ThingSpawner drives
            // its billboard from there too, so mesh and billboard stay in step).
            // Only the armor gem and the barrel actually BLINK; the lamp and
            // the health bonus glow steady, so their blinkAnimation stays null
            // even when the thing has a PickupAnimationTable entry (2014 does —
            // the billboard's star-glint frames are not a glow cadence).
            if (doomedNum == 2035)
                presentation.blinkAnimation = new PickupAnimation(
                    BarrelRules.IdleFrames, BarrelRules.IdleTics);
            else if (doomedNum == 2018 &&
                     PickupAnimationTable.TryGet(doomedNum, out var blinkAnim))
                presentation.blinkAnimation = blinkAnim;
            presentation.brightFrame = blinkBrightFrame;
            presentation.Init(
                resource,
                Mathf.Max(0.01f, heightUnits * worldScale),
                useUnlit,
                emissionStrength,
                pulseMaskResource,
                gemBlink,
                pulseStrength,
                pulseSpeed,
                billboard);
            return presentation.HasModel ? presentation : null;
        }

        /// Native patch heights (WAD pixels) when the accepted mesh should match
        /// the billboard silhouette rather than the (often different) mobjinfo
        /// collision height. Many pickups share height 16 in info.c while their
        /// Freedoom patches differ (STIM 10 / MEDI 20 / SBOX 13 / …).
        static float SpriteHeightPx(int doomedNum, float collisionHeight)
        {
            switch (doomedNum)
            {
                case 2007: return 12f; // CLIPA0
                case 2008: return 10f; // SHELA0
                case 2010: return 24f; // ROCKA0
                case 2011: return 10f; // STIMA0
                case 2012: return 20f; // MEDIA0
                case 2015: return 19f; // BON2A0
                case 2018: return 28f; // ARM1A0
                case 17: return 20f;   // CELPA0
                case 2047: return 12f; // CELLA0
                case 2048: return 16f; // AMMOA0 (matches collision height)
                case 2049: return 13f; // SBOXA0
                case 30: return 56f;   // COL1A0 (collision 48; patch is taller)
                case 31: return 41f;   // COL2A0 (collision 36)
                case 32: return 55f;   // COL3A0 (collision 48)
                case 36: return 49f;   // COL5A0 (collision 36)
                case 43: return 70f;   // TRE1A0
                case 54: return 124f;  // TRE2A0
                case 47: return 69f;   // SMITA0
                default: return collisionHeight;
            }
        }

        static bool TryGetResource(int doomedNum, out string resource)
        {
            switch (doomedNum)
            {
                case 2001:
                    resource = ResourceRoot + "SHOTA0/SHOTA0";
                    return true;
                case 2002:
                    resource = ResourceRoot + "MGUNA0/MGUNA0";
                    return true;
                case 2003:
                    resource = ResourceRoot + "LAUNA0/LAUNA0";
                    return true;
                case 2004:
                    resource = ResourceRoot + "PLASA0/PLASA0";
                    return true;
                case 2005:
                    resource = ResourceRoot + "CSAWA0/CSAWA0";
                    return true;
                case 2006:
                    resource = ResourceRoot + "BFUGA0/BFUGA0";
                    return true;
                case 2007:
                    resource = ResourceRoot + "CLIPA0/CLIPA0";
                    return true;
                case 2008:
                    resource = ResourceRoot + "SHELA0/SHELA0";
                    return true;
                case 2010:
                    resource = ResourceRoot + "ROCKA0/ROCKA0";
                    return true;
                case 2011:
                    resource = ResourceRoot + "STIMA0/STIMA0";
                    return true;
                case 2012:
                    resource = ResourceRoot + "MEDIA0/MEDIA0";
                    return true;
                case 2014:
                    resource = ResourceRoot + "BON1A0/BON1A0";
                    return true;
                case 2015:
                    // Armor bonus mesh; white/yellow stripes pulse via emission.
                    resource = ResourceRoot + "BON2A0/BON2A0";
                    return true;
                case 2018:
                    // Green armor: the ARM1B0-frame mesh was preferred at the
                    // 2026-08-13 gate; gem blinks A/B via emission mask synced
                    // to PickupAnimationTable (6+6 tics).
                    resource = ResourceRoot + "ARM1B0/ARM1B0";
                    return true;
                case 2047:
                    resource = ResourceRoot + "CELLA0/CELLA0";
                    return true;
                case 2048:
                    resource = ResourceRoot + "AMMOA0/AMMOA0";
                    return true;
                case 2049:
                    resource = ResourceRoot + "SBOXA0/SBOXA0";
                    return true;
                case 17:
                    resource = ResourceRoot + "CELPA0/CELPA0";
                    return true;
                case 2028:
                    resource = ResourceRoot + "COLUA0/COLUA0";
                    return true;
                case 30:
                    resource = ResourceRoot + "COL1A0/COL1A0";
                    return true;
                case 31:
                    resource = ResourceRoot + "COL2A0/COL2A0";
                    return true;
                case 32:
                    resource = ResourceRoot + "COL3A0/COL3A0";
                    return true;
                case 36:
                    // Freedoom's COL5 blinks A/B (eye glow) as a billboard; the
                    // mesh is a single frame A — the blink is a two-texel eye
                    // change, not worth a second TRELLIS generation.
                    resource = ResourceRoot + "COL5A0/COL5A0";
                    return true;
                case 2035:
                    // 2026-08-21 re-roll: the B0-frame mesh was preferred (ring
                    // lamps and green band read best); lamps/band flash via the
                    // emission mask on the barrel's own S_BAR1 cadence.
                    resource = ResourceRoot + "BAR1B0/BAR1B0";
                    return true;
                case 43:
                    resource = ResourceRoot + "TRE1A0/TRE1A0";
                    return true;
                case 54:
                    resource = ResourceRoot + "TRE2A0/TRE2A0";
                    return true;
                case 47:
                    resource = ResourceRoot + "SMITA0/SMITA0";
                    return true;
                default:
                    resource = null;
                    return false;
            }
        }

        void Init(
            string resource,
            float targetHeight,
            bool useUnlit,
            float emissionStrength,
            string pulseMaskResource,
            bool enableGemBlink,
            float pulseStrength,
            float pulseSpeed,
            SpriteBillboard sourceBillboard)
        {
            billboard = sourceBillboard;
            billboardRenderer = GetComponent<MeshRenderer>();
            gemBlink = enableGemBlink;

            var prefab = Resources.Load<GameObject>(resource);
            if (prefab == null)
            {
                Debug.LogWarning($"ExperimentalPickupModel: missing Resources model '{resource}'.");
                Destroy(this);
                return;
            }

            modelRoot = Instantiate(prefab, transform);
            modelRoot.name = "Enhanced3DModel";
            modelRoot.transform.localPosition = Vector3.zero;
            modelRoot.transform.localRotation = Quaternion.identity;
            modelRoot.transform.localScale = Vector3.one;

            modelRenderers = modelRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (modelRenderers.Length == 0)
            {
                Debug.LogWarning($"ExperimentalPickupModel: '{resource}' has no renderers.");
                Destroy(modelRoot);
                modelRoot = null;
                Destroy(this);
                return;
            }

            NormalizeToPickup(targetHeight);
            ConfigureMaterials(useUnlit, emissionStrength, pulseMaskResource, pulseStrength, pulseSpeed);
            SettingsController.SettingsApplied += OnSettingsApplied;
            RefreshVisibility(force: true);
            if (gemBlink)
                ApplyGemBlink(force: true);
        }

        void OnSettingsApplied(GameSettingsData _) => RefreshVisibility(force: true);

        void NormalizeToPickup(float targetHeight)
        {
            Bounds bounds = CombinedBounds();
            if (bounds.size.y <= 0.0001f) return;

            float scale = targetHeight / bounds.size.y;
            modelRoot.transform.localScale *= scale;
            bounds = CombinedBounds();

            Vector3 rootPosition = transform.position;
            modelRoot.transform.position += new Vector3(
                rootPosition.x - bounds.center.x,
                rootPosition.y - bounds.min.y,
                rootPosition.z - bounds.center.z);
        }

        Bounds CombinedBounds()
        {
            Bounds bounds = modelRenderers[0].bounds;
            for (int i = 1; i < modelRenderers.Length; i++)
                bounds.Encapsulate(modelRenderers[i].bounds);
            return bounds;
        }

        void ConfigureMaterials(
            bool useUnlit,
            float emissionStrength,
            string pulseMaskResource,
            float pulseStrength,
            float pulseSpeed)
        {
            Shader shader = useUnlit
                ? Resources.Load<Shader>(
                    "ExperimentalPickups/DoomExperimentalPickupUnlit")
                : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            Texture2D pulseMask = string.IsNullOrEmpty(pulseMaskResource)
                ? null
                : Resources.Load<Texture2D>(pulseMaskResource);

            foreach (var renderer in modelRenderers)
            {
                var source = renderer.sharedMaterials;
                var upgraded = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    var material = source[i] != null
                        ? new Material(source[i])
                        : new Material(shader);
                    Texture albedo = material.mainTexture;
                    material.shader = shader;
                    material.mainTexture = albedo;
                    if (useUnlit)
                    {
                        material.SetFloat("_Exposure", 1f);
                        material.SetFloat("_EmissionStrength", emissionStrength);
                        material.SetColor("_ColorTint", Color.white);
                        if (pulseMask != null)
                        {
                            material.SetTexture("_EmissionMask", pulseMask);
                            if (gemBlink)
                            {
                                // Discrete ARM1 A/B gem blink (not MEDIA0 sine).
                                material.SetFloat("_BlinkMode", 1f);
                                material.SetFloat("_Blink", 1f);
                                material.SetFloat("_PulseStrength", 0.85f);
                                material.SetFloat("_PulseSpeed", 0f);
                            }
                            else
                            {
                                material.SetFloat("_BlinkMode", 0f);
                                material.SetFloat("_PulseStrength", pulseStrength);
                                material.SetFloat("_PulseSpeed", pulseSpeed);
                            }
                        }
                    }
                    else
                        material.SetFloat("_Smoothness", 0.15f);
                    // Generated PBR pickups become unreadable in DOOM's dark
                    // sectors with direct lighting alone. Keep their texture
                    // visible while retaining Lit shading and dynamic lights.
                    if (!useUnlit && albedo != null)
                    {
                        material.SetTexture("_EmissionMap", albedo);
                        material.SetColor("_EmissionColor", Color.white * emissionStrength);
                        material.EnableKeyword("_EMISSION");
                    }
                    upgraded[i] = material;
                    ownedMaterials.Add(material);
                }
                renderer.sharedMaterials = upgraded;
            }
        }

        /// SettingsApplied drives visibility once SettingsController is alive;
        /// per-frame polling remains only as a boot/tests fallback while the
        /// controller doesn't exist yet, plus one catch-up refresh on the
        /// frame it appears. Gem blink always tracks the shared level tic.
        void Update()
        {
            if (gemBlink && ModelVisible)
                ApplyGemBlink(force: false);

            bool hasSettings = SettingsController.Instance != null;
            if (hasSettings && settingsControllerSeen) return;
            settingsControllerSeen = hasSettings;
            RefreshVisibility(force: false);
        }

        /// Same phase as PickupAnimator for the thing's own info.c cadence
        /// (ARM1 S_ARM1/S_ARM1A 6+6; barrel S_BAR1 6+6, bright on frame B).
        void ApplyGemBlink(bool force)
        {
            int gameTic = LevelStatsTracker.Instance != null
                ? LevelStatsTracker.Instance.Stats.Tics
                : 0;
            int blink = 1;
            var animation = blinkAnimation;
            if (animation != null &&
                animation.Frames != null && animation.Tics != null &&
                animation.Frames.Length == animation.Tics.Length &&
                animation.Frames.Length > 0)
            {
                int cycle = 0;
                for (int i = 0; i < animation.Tics.Length; i++)
                    cycle += System.Math.Max(1, animation.Tics[i]);
                int phase = cycle > 0 ? ((gameTic % cycle) + cycle) % cycle : 0;
                int next = 0;
                while (next + 1 < animation.Frames.Length)
                {
                    int duration = System.Math.Max(1, animation.Tics[next]);
                    if (phase < duration) break;
                    phase -= duration;
                    next++;
                }
                // ARM1: frame 0 (A) bright; barrel: frame 1 (B) is the flash.
                blink = animation.Frames[next] == brightFrame ? 1 : 0;
            }

            if (!force && blink == lastGemBlink) return;
            lastGemBlink = blink;
            for (int i = 0; i < ownedMaterials.Count; i++)
            {
                var mat = ownedMaterials[i];
                if (mat != null)
                    mat.SetFloat("_Blink", blink);
            }
        }

        /// Barrel explode (and similar one-shots): drop the static 3D mesh and
        /// keep the billboard so BEXP / death frames stay visible in Enhanced.
        public void RevertToBillboard()
        {
            lockedToBillboard = true;
            if (modelRoot != null)
                modelRoot.SetActive(false);
            if (billboardRenderer != null)
                billboardRenderer.enabled = true;
            if (billboard != null)
                billboard.enabled = true;
        }

        void RefreshVisibility(bool force)
        {
            if (lockedToBillboard) return;

            bool useMesh = ResolveUseMesh();
            if (!force && useMesh == lastUseMesh) return;

            ApplyPresentation(useMesh);
        }

        bool ResolveUseMesh()
        {
            // Prefer SettingsController (user intent / hot-toggle). GraphicsModeController
            // may still report Classic while the Enhanced warm coroutine runs.
            GraphicsMode mode;
            bool toggle3D;
            if (SettingsController.Instance != null)
            {
                mode = SettingsController.Instance.Current.GraphicsMode;
                toggle3D = SettingsController.Instance.Current.Enhanced3DObjects;
            }
            else if (GraphicsModeController.Instance != null)
            {
                mode = GraphicsModeController.Instance.Current;
                toggle3D = true;
            }
            else
            {
                return false;
            }

            return ObjectPresentationResolver.Resolve(
                       mode,
                       toggle3D,
                       hasMesh: HasModel,
                       hasDisplayRedraw: false, // mesh path ignores redraw
                       isAnimated: false) == ObjectPresentation.Mesh;
        }

        void ApplyPresentation(bool useMesh)
        {
            lastUseMesh = useMesh;
            if (modelRoot != null)
                modelRoot.SetActive(useMesh);
            if (billboardRenderer != null)
                billboardRenderer.enabled = !useMesh;
            if (billboard != null)
                billboard.enabled = !useMesh;
        }

        /// Test seam: force mesh on/off without going through settings.
        public void SetEnhancedForTest(bool enhanced)
        {
            if (lockedToBillboard) return;
            ApplyPresentation(enhanced);
        }

        void OnDestroy()
        {
            SettingsController.SettingsApplied -= OnSettingsApplied;
            for (int i = 0; i < ownedMaterials.Count; i++)
                if (ownedMaterials[i] != null)
                    Destroy(ownedMaterials[i]);
            ownedMaterials.Clear();
        }
    }
}
