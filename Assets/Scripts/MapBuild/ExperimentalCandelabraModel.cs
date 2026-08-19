using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Doom.Game;
using Doom.MapBuild.Rendering;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Enhanced 3D presentation for the candelabra (thing 35, `CBRA`).
    ///
    /// It looks like a torch's cousin but splits differently: the three fires
    /// sit INSIDE steel lantern cages, so the metal — base, column, arms AND
    /// the bars in front of the fire — is one connected object, and a row
    /// split would have handed the bars to the flame. The metal is therefore a
    /// generated (TRELLIS) mesh with no computed fallback: a candelabra is not
    /// a solid of revolution, and the lathe that carries the firesticks would
    /// smear its arms into a disc.
    ///
    /// The fires keep the torch treatment — mesh for shape, colour re-derived
    /// per fragment by `Doom/ExperimentalTorch` — but there are three of them,
    /// each with its own spine, so each is a separate part placed by the table
    /// the generator writes beside them. Vanilla `CBRA` is a single frame, so
    /// they do not flicker: Classic never did.
    public sealed class ExperimentalCandelabraModel : MonoBehaviour
    {
        public const int DoomEdNum = 35;
        public const string Sprite = "CBRA";
        const string ResourceRoot = "ExperimentalTorches/";
        const string ShaderResource = ResourceRoot + "DoomExperimentalTorch";

        SpriteBillboard billboard;
        MeshRenderer billboardRenderer;
        Transform modelRoot;
        readonly List<Material> ownedMaterials = new List<Material>();
        bool lastUseMesh;
        bool settingsControllerSeen;

        public bool HasModel => modelRoot != null;
        public bool ModelVisible => HasModel && modelRoot.gameObject.activeSelf;
        public Transform ModelRootForTest => modelRoot;
        public int FireCountForTest { get; private set; }

        /// True once the generated metal has been dropped in; until then the
        /// candelabra stays a billboard in every mode.
        public static bool HasGeneratedStand() =>
            Resources.Load<GameObject>(ResourceRoot + Sprite + "/" + Sprite + "_stand_mesh")
            != null;

        public static ExperimentalCandelabraModel TryAttach(
            GameObject thingRoot,
            int doomEdNum,
            SpriteCache cache,
            float worldScale,
            SpriteBillboard billboard)
        {
            if (thingRoot == null || cache == null) return null;
            if (doomEdNum != DoomEdNum) return null;
            if (!ThingTable.TryGet(doomEdNum, out _)) return null;

            string dir = ResourceRoot + Sprite + "/";
            var metal = Resources.Load<GameObject>(dir + Sprite + "_stand_mesh");
            var table = Resources.Load<TextAsset>(dir + Sprite + "_fires");
            if (metal == null || table == null) return null;

            var fires = ParseFires(table.text);
            if (fires.Count == 0) return null;
            foreach (var fire in fires)
            {
                fire.Mesh = Resources.Load<GameObject>(dir + fire.Name);
                fire.Profile = Resources.Load<Texture2D>(dir + fire.Name + "_profile");
                fire.Spine = Resources.Load<Texture2D>(dir + fire.Name + "_spine");
                if (fire.Mesh == null || fire.Profile == null || fire.Spine == null)
                    return null;
            }

            var patch = cache.Get(Sprite, 0, 0);
            if (!patch.IsValid) return null;

            var model = thingRoot.AddComponent<ExperimentalCandelabraModel>();
            model.Init(billboard, metal, fires,
                       // The billboard hangs its quad from the patch's top offset.
                       bottomY: (patch.TopOffset - patch.Height) * worldScale,
                       metalHeight: patch.Height * worldScale,
                       worldScale: worldScale);
            if (!model.HasModel)
            {
                Destroy(model);
                return null;
            }
            return model;
        }

        sealed class Fire
        {
            public string Name;
            public float OffsetX;    // patch pixels from the thing's axis
            public float BottomY;    // patch pixels above the thing's feet
            public float Height;     // patch pixels
            public GameObject Mesh;
            public Texture2D Profile;
            public Texture2D Spine;
        }

        /// One line per fire: "<name> <offsetX> <bottomY> <rows>", all in patch
        /// pixels, written by Tools/make_torch_model.py next to the meshes.
        static List<Fire> ParseFires(string text)
        {
            var result = new List<Fire>();
            if (string.IsNullOrEmpty(text)) return result;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string[] parts = line.Split(' ');
                if (parts.Length != 4) continue;
                if (!float.TryParse(parts[1], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float x) ||
                    !float.TryParse(parts[2], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float y) ||
                    !float.TryParse(parts[3], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float h))
                    continue;
                result.Add(new Fire { Name = parts[0], OffsetX = x, BottomY = y, Height = h });
            }
            return result;
        }

        void Init(SpriteBillboard sourceBillboard, GameObject metal, List<Fire> fires,
                  float bottomY, float metalHeight, float worldScale)
        {
            billboard = sourceBillboard;
            billboardRenderer = GetComponent<MeshRenderer>();

            var shader = Resources.Load<Shader>(ShaderResource);
            var metalShader = Resources.Load<Shader>(
                "ExperimentalPickups/DoomExperimentalPickupUnlit");
            if (shader == null || metalShader == null)
            {
                Debug.LogWarning("ExperimentalCandelabraModel: shader missing.");
                return;
            }

            var rootGo = new GameObject("Enhanced3DCandelabra");
            rootGo.transform.SetParent(transform, worldPositionStays: false);
            rootGo.transform.localPosition = Vector3.zero;
            rootGo.transform.localRotation = Quaternion.identity;
            rootGo.transform.localScale = Vector3.one;
            modelRoot = rootGo.transform;

            if (!SpawnMetal(metal, metalShader, bottomY, metalHeight))
            {
                Destroy(rootGo);
                modelRoot = null;
                return;
            }

            foreach (var fire in fires)
            {
                var pivot = new GameObject(fire.Name);
                pivot.transform.SetParent(modelRoot, worldPositionStays: false);
                pivot.transform.localPosition = new Vector3(
                    fire.OffsetX * worldScale,
                    bottomY + fire.BottomY * worldScale,
                    0f);
                pivot.transform.localRotation = Quaternion.identity;
                pivot.transform.localScale =
                    Vector3.one * Mathf.Max(0.001f, fire.Height * worldScale);

                var instance = Instantiate(fire.Mesh, pivot.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    Debug.LogWarning($"ExperimentalCandelabraModel: {fire.Name} has no renderers.");
                    Destroy(rootGo);
                    modelRoot = null;
                    return;
                }

                var material = new Material(shader);
                material.mainTexture = fire.Profile;
                material.SetTexture("_SpineTex", fire.Spine);
                material.SetFloat("_SpineRange", 0.5f);
                material.SetFloat("_Exposure", 1f);
                ownedMaterials.Add(material);
                foreach (var renderer in renderers)
                {
                    var slots = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    renderer.sharedMaterials = slots;
                }
            }
            FireCountForTest = fires.Count;

            SettingsController.SettingsApplied += OnSettingsApplied;
            RefreshVisibility(force: true);
        }

        bool SpawnMetal(GameObject prefab, Shader shader, float bottomY, float height)
        {
            var pivot = new GameObject("Metal");
            pivot.transform.SetParent(modelRoot, worldPositionStays: false);
            pivot.transform.localPosition = new Vector3(0f, bottomY, 0f);
            pivot.transform.localRotation = Quaternion.identity;
            pivot.transform.localScale = Vector3.one;

            var instance = Instantiate(prefab, pivot.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("ExperimentalCandelabraModel: metal has no renderers.");
                Destroy(pivot);
                return false;
            }

            foreach (var renderer in renderers)
            {
                var source = renderer.sharedMaterials;
                var upgraded = new Material[Mathf.Max(1, source.Length)];
                for (int i = 0; i < upgraded.Length; i++)
                {
                    var origin = i < source.Length ? source[i] : null;
                    var material = origin != null
                        ? new Material(origin) : new Material(shader);
                    Texture albedo = material.mainTexture;
                    material.shader = shader;
                    material.mainTexture = albedo;
                    material.SetFloat("_Exposure", 1f);
                    material.SetFloat("_EmissionStrength", 0f);
                    material.SetColor("_ColorTint", Color.white);
                    ownedMaterials.Add(material);
                    upgraded[i] = material;
                }
                renderer.sharedMaterials = upgraded;
            }

            // A reconstruction comes back at whatever scale it likes, so the
            // metal is measured and fitted to the sprite's own height.
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > 0.0001f)
            {
                instance.transform.localScale *= height / bounds.size.y;
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
            }
            Vector3 anchor = pivot.transform.position;
            instance.transform.position += new Vector3(
                anchor.x - bounds.center.x,
                anchor.y - bounds.min.y,
                anchor.z - bounds.center.z);
            return true;
        }

        // -- Presentation cascade (same shape as the torch model) -------------

        void OnSettingsApplied(GameSettingsData _) => RefreshVisibility(force: true);

        void Update()
        {
            bool hasSettings = SettingsController.Instance != null;
            if (hasSettings && settingsControllerSeen) return;
            settingsControllerSeen = hasSettings;
            RefreshVisibility(force: false);
        }

        void RefreshVisibility(bool force)
        {
            bool useMesh = ResolveUseMesh();
            if (!force && useMesh == lastUseMesh) return;
            ApplyPresentation(useMesh);
        }

        bool ResolveUseMesh()
        {
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
                       hasDisplayRedraw: false,
                       isAnimated: false) == ObjectPresentation.Mesh;
        }

        void ApplyPresentation(bool useMesh)
        {
            lastUseMesh = useMesh;
            if (modelRoot != null)
                modelRoot.gameObject.SetActive(useMesh);
            if (billboardRenderer != null)
                billboardRenderer.enabled = !useMesh;
            if (billboard != null)
                billboard.enabled = !useMesh;
        }

        /// Test seam: force mesh on/off without going through settings.
        public void SetEnhancedForTest(bool enhanced) => ApplyPresentation(enhanced);

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
