using System.Collections.Generic;
using UnityEngine;
using Doom.Game;
using Doom.MapBuild.Rendering;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Experimental TRELLIS.2 presentation for allowlisted things (pickups and
    /// decorations). Gameplay/collision remain on the original root; Enhanced
    /// swaps only the visible billboard for a textured 3D model from Resources.
    public sealed class ExperimentalPickupModel : MonoBehaviour
    {
        const string ResourceRoot = "ExperimentalPickups/";

        SpriteBillboard billboard;
        MeshRenderer billboardRenderer;
        GameObject modelRoot;
        Renderer[] modelRenderers;
        readonly List<Material> ownedMaterials = new List<Material>();
        bool lastEnhanced;

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
                Mathf.Max(0.01f, def.Height * worldScale),
                useUnlit,
                emissionStrength,
                pulseMaskResource,
                billboard);
            return presentation.HasModel ? presentation : null;
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
                case 2012:
                    resource = ResourceRoot + "MEDIA0/MEDIA0";
                    return true;
                case 2014:
                    resource = ResourceRoot + "BON1A0/BON1A0";
                    return true;
                case 2028:
                    resource = ResourceRoot + "COLUA0/COLUA0";
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
            RefreshVisibility(force: true);
        }

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

        void Update() => RefreshVisibility(force: false);

        void RefreshVisibility(bool force)
        {
            bool enhanced = GraphicsModeController.Instance != null &&
                            GraphicsModeController.Instance.Current == GraphicsMode.Enhanced;
            if (!force && enhanced == lastEnhanced) return;

            lastEnhanced = enhanced;
            if (modelRoot != null)
                modelRoot.SetActive(enhanced);
            if (billboardRenderer != null)
                billboardRenderer.enabled = !enhanced;
            if (billboard != null)
                billboard.enabled = !enhanced;
        }

        public void SetEnhancedForTest(bool enhanced)
        {
            lastEnhanced = enhanced;
            if (modelRoot != null)
                modelRoot.SetActive(enhanced);
            if (billboardRenderer != null)
                billboardRenderer.enabled = !enhanced;
            if (billboard != null)
                billboard.enabled = !enhanced;
        }

        void OnDestroy()
        {
            for (int i = 0; i < ownedMaterials.Count; i++)
                if (ownedMaterials[i] != null)
                    Destroy(ownedMaterials[i]);
            ownedMaterials.Clear();
        }
    }
}
