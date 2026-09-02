using System.Collections.Generic;
using UnityEngine;
using Doom.Game;
using Doom.MapBuild.Rendering;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Enhanced 3D presentation for the six firesticks (E1M5 carries one of
    /// each). A torch is two solids of revolution, so both halves are computed
    /// from the sprite by Tools/make_torch_model.py rather than generated:
    ///
    ///   * the STAND is a turned object — pole, rings, bowl, base — and its
    ///     shading is radial for the same reason a cylinder's is, so one lathe
    ///     mesh plus one colour table reproduce it from every angle;
    ///   * the FLAME is a plume per animation frame (A-D). Its hot core lives
    ///     INSIDE the volume, so the colour is re-derived per fragment by
    ///     `Doom/ExperimentalTorch` instead of being baked on the surface —
    ///     the fireball finding of 2026-08-19, one dimension richer.
    ///
    /// Gameplay, collision and save identity stay on the original thing root:
    /// this component only swaps what is visible.
    public sealed class ExperimentalTorchModel : MonoBehaviour
    {
        const string ResourceRoot = "ExperimentalTorches/";
        const string ShaderResource = ResourceRoot + "DoomExperimentalTorch";

        /// Vanilla flame cadence (info.c S_TBLUETORCH…/S_SMBTORCH…): four
        /// frames, four tics each.
        public const int FrameTics = 4;
        public const int FrameCount = 4;

        static readonly Dictionary<int, string> Routed = new Dictionary<int, string>
        {
            { 44, "TBLU" }, { 45, "TGRN" }, { 46, "TRED" },
            { 55, "SMBT" }, { 56, "SMGT" }, { 57, "SMRT" },
        };

        SpriteBillboard billboard;
        MeshRenderer billboardRenderer;
        Transform modelRoot;
        GameObject[] flameFrames;
        readonly List<Material> ownedMaterials = new List<Material>();
        int currentFrame = -1;
        int testTic = -1;
        bool lastUseMesh;
        bool settingsControllerSeen;
        bool usesTrellisStand;

        public bool HasModel => modelRoot != null;
        public bool ModelVisible => HasModel && modelRoot.gameObject.activeSelf;
        public int CurrentFrameForTest => currentFrame;
        public Transform ModelRootForTest => modelRoot;
        public bool UsesGeneratedStandForTest => usesTrellisStand;

        /// True when a generated (TRELLIS) stand has been dropped in for this
        /// sprite; the computed lathe stand carries it until then.
        public static bool HasGeneratedStand(string sprite) =>
            sprite != null &&
            Resources.Load<GameObject>(ResourceRoot + sprite + "/" + sprite + "_stand_mesh") != null;

        public static bool IsRoutedForTest(int doomEdNum) => Routed.ContainsKey(doomEdNum);

        public static IEnumerable<KeyValuePair<int, string>> RoutedForTest => Routed;

        /// Attach only when the stand and EVERY flame frame are on disk —
        /// partial coverage would swap presentation mid-flicker. Heights come
        /// from the WAD patch the billboard would have drawn, so the 3D torch
        /// occupies exactly the sprite's place in the world.
        public static ExperimentalTorchModel TryAttach(
            GameObject thingRoot,
            int doomEdNum,
            SpriteCache cache,
            float worldScale,
            SpriteBillboard billboard)
        {
            if (thingRoot == null || cache == null) return null;
            if (!Routed.TryGetValue(doomEdNum, out string sprite)) return null;
            if (!ThingTable.TryGet(doomEdNum, out _)) return null;

            string dir = ResourceRoot + sprite + "/";
            var standMesh = Resources.Load<GameObject>(dir + sprite + "_stand");
            var standProfile = Resources.Load<Texture2D>(dir + sprite + "_stand_profile");
            var standSpine = Resources.Load<Texture2D>(dir + sprite + "_stand_spine");
            if (standMesh == null || standProfile == null || standSpine == null)
                return null;

            // A generated stand outranks the computed one when it exists: the
            // lathe reproduces the silhouette exactly but turns the head's
            // frontal ornament into a ring, which only modelled geometry can
            // carry. The flame stays computed either way — no bake can hold a
            // gradient whose core is inside the volume.
            var trellisStand = Resources.Load<GameObject>(dir + sprite + "_stand_mesh");

            var flameMeshes = new GameObject[FrameCount];
            var flameProfiles = new Texture2D[FrameCount];
            var flameSpines = new Texture2D[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                string lump = sprite + (char)('A' + i) + "0_flame";
                flameMeshes[i] = Resources.Load<GameObject>(dir + lump);
                flameProfiles[i] = Resources.Load<Texture2D>(dir + lump + "_profile");
                flameSpines[i] = Resources.Load<Texture2D>(dir + lump + "_spine");
                if (flameMeshes[i] == null || flameProfiles[i] == null ||
                    flameSpines[i] == null)
                    return null;
            }

            var patch = cache.Get(sprite, 0, 0);
            if (!patch.IsValid) return null;

            // The colour tables have one row per sprite row, so the split
            // between flame and stand is carried by the assets themselves.
            int flameRows = flameProfiles[0].height;
            int standRows = patch.Height - flameRows;
            if (flameRows <= 0 || standRows <= 0) return null;

            var model = thingRoot.AddComponent<ExperimentalTorchModel>();
            // The billboard hangs its quad from the patch's top offset.
            float bottomY = (patch.TopOffset - patch.Height) * worldScale;
            model.Init(
                billboard,
                standMesh, standProfile, standSpine, trellisStand,
                flameMeshes, flameProfiles, flameSpines,
                bottomY: bottomY,
                standHeight: standRows * worldScale,
                flameHeight: flameRows * worldScale);
            if (!model.HasModel)
            {
                Destroy(model);
                return null;
            }
            return model;
        }

        void Init(
            SpriteBillboard sourceBillboard,
            GameObject standMesh, Texture2D standProfile, Texture2D standSpine,
            GameObject trellisStand,
            GameObject[] flameMeshes, Texture2D[] flameProfiles, Texture2D[] flameSpines,
            float bottomY, float standHeight, float flameHeight)
        {
            billboard = sourceBillboard;
            billboardRenderer = GetComponent<MeshRenderer>();

            var shader = Resources.Load<Shader>(ShaderResource);
            if (shader == null)
            {
                Debug.LogWarning("ExperimentalTorchModel: torch shader missing.");
                return;
            }

            var rootGo = new GameObject("Enhanced3DTorch");
            rootGo.transform.SetParent(transform, worldPositionStays: false);
            rootGo.transform.localPosition = Vector3.zero;
            rootGo.transform.localRotation = Quaternion.identity;
            rootGo.transform.localScale = Vector3.one;
            modelRoot = rootGo.transform;

            bool standOk = trellisStand != null
                ? SpawnTrellisStand(trellisStand, bottomY, standHeight)
                : Spawn(standMesh, standProfile, standSpine, shader,
                        bottomY, standHeight, "Stand", out _);
            if (!standOk)
            {
                Destroy(rootGo);
                modelRoot = null;
                return;
            }
            usesTrellisStand = trellisStand != null;

            flameFrames = new GameObject[flameMeshes.Length];
            for (int i = 0; i < flameMeshes.Length; i++)
            {
                if (!Spawn(flameMeshes[i], flameProfiles[i], flameSpines[i], shader,
                           bottomY + standHeight, flameHeight, "Flame" + (char)('A' + i),
                           out flameFrames[i]))
                {
                    Destroy(rootGo);
                    modelRoot = null;
                    return;
                }
                flameFrames[i].SetActive(false);
            }

            ApplyFrame(FrameForTic(CurrentTic()));
            SettingsController.SettingsApplied += OnSettingsApplied;
            RefreshVisibility(force: true);
        }

        /// A generated stand carries its own baked albedo, so it goes through
        /// the pickup's unlit shader and is measured, not assumed: TRELLIS
        /// meshes come back at whatever scale the reconstruction chose.
        bool SpawnTrellisStand(GameObject prefab, float bottomY, float height)
        {
            var pivotGo = new GameObject("Stand");
            pivotGo.transform.SetParent(modelRoot, worldPositionStays: false);
            pivotGo.transform.localPosition = new Vector3(0f, bottomY, 0f);
            pivotGo.transform.localRotation = Quaternion.identity;
            pivotGo.transform.localScale = Vector3.one;

            var instance = Instantiate(prefab, pivotGo.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var renderers = instance.GetComponentsInChildren<Renderer>(
                includeInactive: true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("ExperimentalTorchModel: generated stand has no renderers.");
                Destroy(pivotGo);
                return false;
            }

            var shader = Resources.Load<Shader>(
                "ExperimentalPickups/DoomExperimentalPickupUnlit");
            if (shader == null)
            {
                Debug.LogWarning("ExperimentalTorchModel: pickup shader missing.");
                Destroy(pivotGo);
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

            // Scale to the sprite's own stand height, then sit the mesh on the
            // floor with its axis on the thing's axis.
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
            Vector3 anchor = pivotGo.transform.position;
            instance.transform.position += new Vector3(
                anchor.x - bounds.center.x,
                anchor.y - bounds.min.y,
                anchor.z - bounds.center.z);
            return true;
        }

        /// One part: the OBJ is normalized (axis at x=z=0, bottom at y=0,
        /// height 1.0), so the scale IS the part's height in world units and
        /// no bounds measuring is needed.
        bool Spawn(GameObject prefab, Texture2D profile, Texture2D spine, Shader shader,
                   float localY, float height, string name, out GameObject pivotGo)
        {
            pivotGo = new GameObject(name);
            pivotGo.transform.SetParent(modelRoot, worldPositionStays: false);
            pivotGo.transform.localPosition = new Vector3(0f, localY, 0f);
            pivotGo.transform.localRotation = Quaternion.identity;
            pivotGo.transform.localScale = Vector3.one * Mathf.Max(0.001f, height);

            var instance = Instantiate(prefab, pivotGo.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var renderers = instance.GetComponentsInChildren<Renderer>(
                includeInactive: true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"ExperimentalTorchModel: '{name}' has no renderers.");
                Destroy(pivotGo);
                pivotGo = null;
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
                var slots = new Material[renderer.sharedMaterials.Length == 0
                    ? 1 : renderer.sharedMaterials.Length];
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
            }
            return true;
        }

        // -- Vanilla flame cadence -------------------------------------------

        int CurrentTic() => testTic >= 0
            ? testTic
            : (LevelStatsTracker.Instance != null
                ? LevelStatsTracker.Instance.Stats.Tics
                : 0);

        public static int FrameForTic(int gameTic)
        {
            int cycle = FrameCount * FrameTics;
            int phase = ((gameTic % cycle) + cycle) % cycle;
            return phase / FrameTics;
        }

        void ApplyFrame(int frame)
        {
            if (flameFrames == null || frame == currentFrame) return;
            currentFrame = frame;
            for (int i = 0; i < flameFrames.Length; i++)
                if (flameFrames[i] != null)
                    flameFrames[i].SetActive(i == frame);
        }

        /// Test seam: drive the flicker without a LevelStatsTracker.
        public void AdvanceToTicForTest(int gameTic)
        {
            testTic = gameTic;
            ApplyFrame(FrameForTic(gameTic));
        }

        // -- Presentation cascade (same shape as the pickup/projectile models) --

        void OnSettingsApplied(GameSettingsData _) => RefreshVisibility(force: true);

        void Update()
        {
            if (ModelVisible)
                ApplyFrame(FrameForTic(CurrentTic()));

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
                       isAnimated: true) == ObjectPresentation.Mesh;
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
