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
            float emissionStrength = doomedNum == 2014 ? 0.65f : 0f;
            string pulseMaskResource = doomedNum == 2012
                ? ResourceRoot + "MEDIA0/MEDIA0_emission"
                : null;
            presentation.Init(
                resource,
                Mathf.Max(0.01f, heightUnits * worldScale),
                useUnlit,
                emissionStrength,
                pulseMaskResource,
                billboard);
            return presentation.HasModel ? presentation : null;
        }

        /// Native patch heights (WAD pixels) for tree decorations whose sprite
        /// stands taller than the mobjinfo collision height. Other things keep
        /// the collision height that their accepted meshes were tuned to.
        static float SpriteHeightPx(int doomedNum, float collisionHeight)
        {
            switch (doomedNum)
            {
                case 43: return 70f;  // TRE1A0
                case 54: return 124f; // TRE2A0
                case 47: return 69f;  // SMITA0
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
                case 2018:
                    // Green armor mesh generated from the B blink frame
                    // (ARM1B0 conditioning accepted 2026-08-10); the static
                    // mesh covers both A/B billboard frames like BAR1.
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
                case 2035:
                    resource = ResourceRoot + "BAR1A0/BAR1A0";
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
            SpriteBillboard sourceBillboard)
        {
            billboard = sourceBillboard;
            billboardRenderer = GetComponent<MeshRenderer>();

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
            ConfigureMaterials(useUnlit, emissionStrength, pulseMaskResource);
            SettingsController.SettingsApplied += OnSettingsApplied;
            RefreshVisibility(force: true);
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
            string pulseMaskResource)
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
                            material.SetFloat("_PulseStrength", 1.2f);
                            material.SetFloat("_PulseSpeed", 8f);
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
        /// frame it appears.
        void Update()
        {
            bool hasSettings = SettingsController.Instance != null;
            if (hasSettings && settingsControllerSeen) return;
            settingsControllerSeen = hasSettings;
            RefreshVisibility(force: false);
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
