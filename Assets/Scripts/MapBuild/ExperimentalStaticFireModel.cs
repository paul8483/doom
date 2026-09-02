using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Doom.Game;
using Doom.MapBuild.Rendering;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Enhanced 3D presentation for lights whose fire does NOT animate — the
    /// candelabra (35, `CBRA`) and the candle (34, `CAND`). Vanilla gives both
    /// a single frame, so unlike the firesticks they never flicker: Classic
    /// never did either.
    ///
    /// Both are a body plus one or more anchored fires, and the fires keep the
    /// torch treatment — mesh for shape, colour re-derived per fragment by
    /// `Doom/ExperimentalTorch` — one part each, because a single table holds
    /// one axis offset per height and the candelabra's three fires sit at
    /// three different x.
    ///
    /// The bodies differ in what they may be built from:
    ///   * `CBRA`'s metal — base, column, arms AND the cage bars that cross in
    ///     front of the fire — is one connected object that is NOT a solid of
    ///     revolution, so it is a generated (TRELLIS) mesh with no computed
    ///     fallback: the lathe that carries the firesticks would smear its
    ///     arms into a disc;
    ///   * `CAND`'s wax IS a cylinder, so it is computed like a torch stand
    ///     and needs nothing generated at all.
    public sealed class ExperimentalStaticFireModel : MonoBehaviour
    {
        const string ResourceRoot = "ExperimentalTorches/";
        const string ShaderResource = ResourceRoot + "DoomExperimentalTorch";
        const string PickupShader = "ExperimentalPickups/DoomExperimentalPickupUnlit";

        /// doomednum -> (sprite, may the body be computed as a lathe?).
        static readonly Dictionary<int, (string Sprite, bool LatheBody)> Routed =
            new Dictionary<int, (string, bool)>
            {
                { 35, ("CBRA", false) },
                { 34, ("CAND", true) },
            };

        public static IEnumerable<KeyValuePair<int, (string Sprite, bool LatheBody)>>
            RoutedForTest => Routed;

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

        /// True when this light's body can be built at all: a generated mesh,
        /// or a computed lathe where the shape allows one. Until then the thing
        /// stays a billboard in every mode.
        public static bool HasBody(int doomEdNum)
        {
            if (!Routed.TryGetValue(doomEdNum, out var route)) return false;
            string dir = ResourceRoot + route.Sprite + "/";
            if (Resources.Load<GameObject>(dir + route.Sprite + "_stand_mesh") != null)
                return true;
            return route.LatheBody
                && Resources.Load<GameObject>(dir + route.Sprite + "_stand") != null;
        }

        public static ExperimentalStaticFireModel TryAttach(
            GameObject thingRoot,
            int doomEdNum,
            SpriteCache cache,
            float worldScale,
            SpriteBillboard billboard)
        {
            if (thingRoot == null || cache == null) return null;
            if (!Routed.TryGetValue(doomEdNum, out var route)) return null;
            if (!ThingTable.TryGet(doomEdNum, out _)) return null;

            string sprite = route.Sprite;
            string dir = ResourceRoot + sprite + "/";
            var table = Resources.Load<TextAsset>(dir + sprite + "_fires");
            if (table == null) return null;

            // A generated body wins; a lathe carries the shapes that are
            // solids of revolution, and nothing else may fall back at all.
            var generated = Resources.Load<GameObject>(dir + sprite + "_stand_mesh");
            GameObject lathe = null;
            Texture2D latheProfile = null, latheSpine = null;
            if (generated == null)
            {
                if (!route.LatheBody) return null;
                lathe = Resources.Load<GameObject>(dir + sprite + "_stand");
                latheProfile = Resources.Load<Texture2D>(dir + sprite + "_stand_profile");
                latheSpine = Resources.Load<Texture2D>(dir + sprite + "_stand_spine");
                if (lathe == null || latheProfile == null || latheSpine == null)
                    return null;
            }

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

            var patch = cache.Get(sprite, 0, 0);
            if (!patch.IsValid) return null;

            // A generated body was reconstructed from the whole sprite, so it
            // spans the whole patch; a lathe body only reaches as high as the
            // rows its own colour table was built from.
            float bodyRows = generated != null ? patch.Height : latheProfile.height;

            var model = thingRoot.AddComponent<ExperimentalStaticFireModel>();
            model.Init(billboard, generated, lathe, latheProfile, latheSpine, fires,
                       // The billboard hangs its quad from the patch's top offset.
                       bottomY: (patch.TopOffset - patch.Height) * worldScale,
                       bodyHeight: bodyRows * worldScale,
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

        void Init(SpriteBillboard sourceBillboard,
                  GameObject generated, GameObject lathe,
                  Texture2D latheProfile, Texture2D latheSpine,
                  List<Fire> fires,
                  float bottomY, float bodyHeight, float worldScale)
        {
            billboard = sourceBillboard;
            billboardRenderer = GetComponent<MeshRenderer>();

            var shader = Resources.Load<Shader>(ShaderResource);
            var metalShader = Resources.Load<Shader>(PickupShader);
            if (shader == null || metalShader == null)
            {
                Debug.LogWarning("ExperimentalStaticFireModel: shader missing.");
                return;
            }

            var rootGo = new GameObject("Enhanced3DStaticFire");
            rootGo.transform.SetParent(transform, worldPositionStays: false);
            rootGo.transform.localPosition = Vector3.zero;
            rootGo.transform.localRotation = Quaternion.identity;
            rootGo.transform.localScale = Vector3.one;
            modelRoot = rootGo.transform;

            bool bodyOk = generated != null
                ? SpawnGeneratedBody(generated, metalShader, bottomY, bodyHeight)
                : SpawnLatheBody(lathe, latheProfile, latheSpine, shader,
                                 bottomY, bodyHeight);
            if (!bodyOk)
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
                    Debug.LogWarning($"ExperimentalStaticFireModel: {fire.Name} has no renderers.");
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

        /// The computed body: the OBJ is normalized (axis at x=z=0, bottom at
        /// y=0, height 1.0), so the scale IS its height and nothing is measured.
        bool SpawnLatheBody(GameObject prefab, Texture2D profile, Texture2D spine,
                            Shader shader, float bottomY, float height)
        {
            var pivot = new GameObject("Body");
            pivot.transform.SetParent(modelRoot, worldPositionStays: false);
            pivot.transform.localPosition = new Vector3(0f, bottomY, 0f);
            pivot.transform.localRotation = Quaternion.identity;
            pivot.transform.localScale = Vector3.one * Mathf.Max(0.001f, height);

            var instance = Instantiate(prefab, pivot.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("ExperimentalStaticFireModel: body has no renderers.");
                Destroy(pivot);
                return false;
            }

            var material = new Material(shader);
            material.mainTexture = profile;
            material.SetTexture("_SpineTex", spine);
            material.SetFloat("_SpineRange", 0.5f);
            material.SetFloat("_Exposure", 1f);
            ownedMaterials.Add(material);
            foreach (var renderer in renderers)
            {
                var slots = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
            }
            return true;
        }

        bool SpawnGeneratedBody(GameObject prefab, Shader shader, float bottomY,
                                float height)
        {
            var pivot = new GameObject("Body");
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
                Debug.LogWarning("ExperimentalStaticFireModel: body has no renderers.");
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
            if (SettingsController.Instance != null)
            {
                mode = SettingsController.Instance.Current.GraphicsMode;
            }
            else if (GraphicsModeController.Instance != null)
            {
                mode = GraphicsModeController.Instance.Current;
            }
            else
            {
                return false;
            }

            return ObjectPresentationResolver.Resolve(
                       mode,
                       hasMesh: HasModel,
                       hasDisplayRedraw: false,
                       isAnimated: false) == ObjectPresentation.Mesh;
        }

        void ApplyPresentation(bool useMesh)
        {
            lastUseMesh = useMesh;
            // Undo the billboard's camera-facing yaw on the shared root (see
            // ExperimentalPickupModel.ApplyPresentation).
            if (useMesh)
                transform.rotation = Quaternion.identity;
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
