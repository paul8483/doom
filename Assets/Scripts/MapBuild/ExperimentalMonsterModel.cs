using System.Collections.Generic;
using UnityEngine;
using Doom.Game;
using Doom.MapBuild.Rendering;

namespace Doom.MapBuild
{
    /// Stop-motion TRELLIS.2 presentation for allowlisted monsters: one
    /// doomified mesh per live animation frame (stand/walk/attack/pain),
    /// switched on the 35 Hz brain tick like sprite frames. Death, gibs and
    /// corpse revert to the billboard permanently (barrel BEXP pattern).
    /// Gameplay, collision and save identity stay on the original thing
    /// root; Classic and Enhanced+3D Off keep the sprite billboard.
    public sealed class ExperimentalMonsterModel : MonoBehaviour
    {
        const string ResourceRoot = "ExperimentalMonsters/";
        const float PoseInterpRate = 35f;

        sealed class MonsterModelSet
        {
            public readonly string Sprite;
            public readonly string[] FrameLetters;
            public readonly float[] PatchHeightsPx;
            // TRELLIS front view looks down -Z after Unity import; calibrated
            // per set at the import gate.
            public readonly float YawOffsetDeg;

            public MonsterModelSet(string sprite, string[] letters,
                                   float[] heightsPx, float yawOffsetDeg)
            {
                Sprite = sprite;
                FrameLetters = letters;
                PatchHeightsPx = heightsPx;
                YawOffsetDeg = yawOffsetDeg;
            }
        }

        // Native patch heights (px) mirror the WAD patch headers per frame —
        // the billboard renders each frame at patch size, so per-frame mesh
        // normalization keeps the exact same silhouette scale behaviour.
        static readonly Dictionary<string, MonsterModelSet> Sets = new()
        {
            ["POSS"] = new MonsterModelSet(
                "POSS",
                new[] { "A", "B", "C", "D", "E", "F", "G" },
                new[] { 57f, 57f, 57f, 57f, 56f, 56f, 55f },
                yawOffsetDeg: 0f),
            // Attaches only once all 7 frame meshes land in Resources
            // (TryAttach is all-or-nothing), so listing ahead is safe.
            ["SPOS"] = new MonsterModelSet(
                "SPOS",
                new[] { "A", "B", "C", "D", "E", "F", "G" },
                new[] { 55f, 55f, 56f, 56f, 56f, 56f, 55f },
                yawOffsetDeg: 0f),
            // Demon: melee attack spans E-F-G, pain is H (8 live frames).
            // The spectre (58) never routes here — ThingSpawner keeps it on
            // the MF_SHADOW billboard.
            ["SARG"] = new MonsterModelSet(
                "SARG",
                new[] { "A", "B", "C", "D", "E", "F", "G", "H" },
                new[] { 59f, 59f, 59f, 59f, 60f, 60f, 60f, 50f },
                yawOffsetDeg: 0f),
            // Imp: attack spans E-F-G (fireball launches on G), pain is H.
            // Offset stays 0 like every monster (all TRELLIS meshes share
            // the same forward): the 2026-08-14 «walks back-first» reports
            // were the FACE being unreadable (eyes lost to quantization) —
            // fixed by the eye-boost in project_hint_texture, not by yaw.
            ["TROO"] = new MonsterModelSet(
                "TROO",
                new[] { "A", "B", "C", "D", "E", "F", "G", "H" },
                new[] { 60f, 62f, 60f, 62f, 62f, 61f, 64f, 63f },
                yawOffsetDeg: 0f),
        };

        MonsterModelSet set;
        SpriteBillboard billboard;
        MeshRenderer billboardRenderer;
        Transform yawPivot;
        GameObject[] frameModels;
        readonly List<Material> ownedMaterials = new List<Material>();

        int currentFrame;
        bool reverted;
        bool lastUseMesh;
        bool settingsControllerSeen;

        // Gameplay pose interpolation (mirrors SpriteBillboard's opt-in interp:
        // MonsterController moves the transform in 35 Hz steps).
        bool poseSeeded;
        Vector3 prevPos, currPos;
        float prevAngleDeg, currAngleDeg;
        float poseAlpha = 1f;

        public bool HasModel => yawPivot != null;
        public bool ModelVisible => HasModel && yawPivot.gameObject.activeSelf;
        public int CurrentFrameForTest => currentFrame;
        public bool RevertedForTest => reverted;

        /// Attach when every live frame of the sprite has an accepted mesh —
        /// all-or-nothing, same rule as the SpriteCache animation gate for
        /// display redraws (partial coverage would flicker between styles).
        public static ExperimentalMonsterModel TryAttach(
            GameObject monsterRoot,
            string sprite,
            float worldScale,
            SpriteBillboard billboard)
        {
            if (monsterRoot == null || sprite == null) return null;
            if (!Sets.TryGetValue(sprite, out var set)) return null;

            var prefabs = new GameObject[set.FrameLetters.Length];
            var emissionMasks = new Texture2D[set.FrameLetters.Length];
            bool allMasks = true;
            for (int i = 0; i < set.FrameLetters.Length; i++)
            {
                string resource =
                    ResourceRoot + set.Sprite + "/" + set.Sprite + set.FrameLetters[i] + "1";
                prefabs[i] = Resources.Load<GameObject>(resource);
                if (prefabs[i] == null) return null;
                // Optional steady-glow masks (SPOS visor). All-or-nothing like
                // the frame meshes: a partial set would strobe during walk.
                emissionMasks[i] = Resources.Load<Texture2D>(resource + "_emission");
                if (emissionMasks[i] == null) allMasks = false;
            }
            if (!allMasks)
                emissionMasks = null;

            var model = monsterRoot.AddComponent<ExperimentalMonsterModel>();
            model.Init(set, prefabs, emissionMasks, worldScale, billboard);
            if (!model.HasModel)
            {
                Destroy(model);
                return null;
            }
            return model;
        }

        void Init(MonsterModelSet set, GameObject[] prefabs,
                  Texture2D[] emissionMasks, float worldScale,
                  SpriteBillboard sourceBillboard)
        {
            this.set = set;
            billboard = sourceBillboard;
            billboardRenderer = GetComponent<MeshRenderer>();

            // Meshes hang off a dedicated pivot so facing rotation spins them
            // around the thing's vertical axis regardless of the billboard's
            // camera-facing rotation on the shared root transform.
            var pivotGo = new GameObject("Enhanced3DMonster");
            pivotGo.transform.SetParent(transform, worldPositionStays: false);
            pivotGo.transform.localPosition = Vector3.zero;
            pivotGo.transform.rotation = Quaternion.identity;
            yawPivot = pivotGo.transform;

            frameModels = new GameObject[prefabs.Length];
            for (int i = 0; i < prefabs.Length; i++)
            {
                var instance = Instantiate(prefabs[i], yawPivot);
                instance.name = set.Sprite + set.FrameLetters[i] + "1";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                var renderers = instance.GetComponentsInChildren<Renderer>(includeInactive: true);
                if (renderers.Length == 0)
                {
                    Debug.LogWarning(
                        $"ExperimentalMonsterModel: '{instance.name}' has no renderers.");
                    Destroy(pivotGo);
                    yawPivot = null;
                    return;
                }
                NormalizeFrame(instance, renderers,
                               set.PatchHeightsPx[i] * worldScale);
                ConfigureMaterials(renderers,
                                   emissionMasks != null ? emissionMasks[i] : null);
                instance.SetActive(false);
                frameModels[i] = instance;
            }

            currentFrame = 0;
            currPos = prevPos = transform.position;
            currAngleDeg = prevAngleDeg =
                billboard != null ? billboard.DoomAngleDegrees : 0f;
            poseSeeded = true;

            SettingsController.SettingsApplied += OnSettingsApplied;
            RefreshVisibility(force: true);
        }

        /// Scale by the frame's native patch height and anchor feet at the
        /// pivot origin, bounds-centered on XZ (same anchor the billboard
        /// quad uses for floor things).
        void NormalizeFrame(GameObject instance, Renderer[] renderers, float targetHeight)
        {
            Bounds bounds = CombinedBounds(renderers);
            if (bounds.size.y <= 0.0001f) return;

            float scale = targetHeight / bounds.size.y;
            instance.transform.localScale *= scale;
            bounds = CombinedBounds(renderers);

            Vector3 pivotPos = yawPivot.position;
            instance.transform.position += new Vector3(
                pivotPos.x - bounds.center.x,
                pivotPos.y - bounds.min.y,
                pivotPos.z - bounds.center.z);
        }

        static Bounds CombinedBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// Same presentation stack as pickups/decorations: Resources-loaded
        /// unlit shader (exposure 1.0, SectorFog aware). An optional emission
        /// mask lights masked texels steadily in the albedo's own hue (blue
        /// visor) — discrete-blink path with _Blink pinned to 1.
        void ConfigureMaterials(Renderer[] renderers, Texture2D emissionMask)
        {
            Shader shader = Resources.Load<Shader>(
                "ExperimentalPickups/DoomExperimentalPickupUnlit");
            if (shader == null) return;

            foreach (var renderer in renderers)
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
                    material.SetFloat("_Exposure", 1f);
                    material.SetFloat("_EmissionStrength", 0f);
                    material.SetColor("_ColorTint", Color.white);
                    if (emissionMask != null)
                    {
                        material.SetTexture("_EmissionMask", emissionMask);
                        material.SetFloat("_BlinkMode", 1f);
                        material.SetFloat("_Blink", 1f);
                        material.SetFloat("_PulseStrength", 0.9f);
                        material.SetFloat("_PulseSpeed", 0f);
                    }
                    upgraded[i] = material;
                    ownedMaterials.Add(material);
                }
                renderer.sharedMaterials = upgraded;
            }
        }

        // ── Seams called from MonsterController ──────────────────────────────

        /// Brain frame switch. Live frames (stand/run/attack/pain) select the
        /// matching mesh; any frame outside coverage (death H+, xdeath) drops
        /// to the billboard for good.
        public void NotifyFrame(int frame)
        {
            if (reverted || frameModels == null) return;
            if (frame < 0 || frame >= frameModels.Length)
            {
                RevertToBillboard();
                return;
            }
            if (frame == currentFrame) return;
            if (ModelVisible)
            {
                frameModels[currentFrame].SetActive(false);
                frameModels[frame].SetActive(true);
            }
            currentFrame = frame;
        }

        /// 35 Hz gameplay pose from MonsterController (position step + facing),
        /// interpolated visually like the billboard's Enhanced pose interp.
        public void NotifyGameplayPose(Vector3 pos, float doomAngleDegrees)
        {
            if (!poseSeeded)
            {
                prevPos = currPos = pos;
                prevAngleDeg = currAngleDeg = doomAngleDegrees;
                poseSeeded = true;
                poseAlpha = 1f;
                return;
            }
            prevPos = currPos;
            prevAngleDeg = currAngleDeg;
            currPos = pos;
            currAngleDeg = doomAngleDegrees;
            poseAlpha = 0f;
        }

        /// Death / gib / corpse / save-restore-dead: hide the mesh forever and
        /// hand presentation back to the billboard (barrel BEXP pattern).
        public void RevertToBillboard()
        {
            reverted = true;
            if (yawPivot != null)
                yawPivot.gameObject.SetActive(false);
            if (billboardRenderer != null)
                billboardRenderer.enabled = true;
            if (billboard != null)
                billboard.enabled = true;
        }

        // ── Presentation cascade (same shape as ExperimentalPickupModel) ─────

        void OnSettingsApplied(GameSettingsData _) => RefreshVisibility(force: true);

        void Update()
        {
            bool hasSettings = SettingsController.Instance != null;
            if (hasSettings && settingsControllerSeen) return;
            settingsControllerSeen = hasSettings;
            RefreshVisibility(force: false);
        }

        void LateUpdate()
        {
            if (!ModelVisible) return;

            poseAlpha = Mathf.Clamp01(poseAlpha + Time.deltaTime * PoseInterpRate);
            float renderAngle = Mathf.LerpAngle(prevAngleDeg, currAngleDeg, poseAlpha);
            Vector3 visualPos = Vector3.Lerp(prevPos, currPos, poseAlpha);

            // DOOM angle is CCW from East; Unity yaw is CW from +Z (North).
            yawPivot.rotation =
                Quaternion.Euler(0f, 90f - renderAngle + set.YawOffsetDeg, 0f);
            yawPivot.position = transform.position + (visualPos - currPos);
        }

        void RefreshVisibility(bool force)
        {
            if (reverted) return;

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
                       isAnimated: true) == ObjectPresentation.Mesh;
        }

        void ApplyPresentation(bool useMesh)
        {
            lastUseMesh = useMesh;
            if (yawPivot != null)
            {
                yawPivot.gameObject.SetActive(useMesh);
                if (useMesh && frameModels != null)
                    for (int i = 0; i < frameModels.Length; i++)
                        frameModels[i].SetActive(i == currentFrame);
            }
            if (billboardRenderer != null)
                billboardRenderer.enabled = !useMesh;
            if (billboard != null)
                billboard.enabled = !useMesh;
        }

        /// Test seam: force mesh on/off without going through settings.
        public void SetEnhancedForTest(bool enhanced)
        {
            if (reverted) return;
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
