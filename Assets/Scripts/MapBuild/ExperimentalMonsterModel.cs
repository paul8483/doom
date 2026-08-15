using System.Collections.Generic;
using UnityEngine;
using Doom.Game;
using Doom.MapBuild.Rendering;

namespace Doom.MapBuild
{
    /// Stop-motion TRELLIS.2 presentation for allowlisted monsters: one
    /// doomified mesh per animation frame, switched on the 35 Hz brain tick
    /// like sprite frames. Live frames (stand/walk/attack/pain) are covered
    /// per monster; the death sequence and the corpse are covered too when
    /// their whole mesh set is present, otherwise death falls back to the
    /// billboard permanently (barrel BEXP pattern). Gibs (XDEATH) always
    /// revert. Gameplay, collision and save identity stay on the original
    /// thing root; Classic and Enhanced+3D Off keep the sprite billboard.
    public sealed class ExperimentalMonsterModel : MonoBehaviour
    {
        const string ResourceRoot = "ExperimentalMonsters/";
        const float PoseInterpRate = 35f;

        sealed class MonsterModelSet
        {
            public readonly string Sprite;
            // Full lump names per brain frame index — live frames carry
            // rotation 1 (POSSA1), death frames are rotation-less (POSSH0).
            public readonly string[] FrameLumps;
            public readonly float[] PatchHeightsPx;
            /// Frames [0, LiveFrameCount) are stand/walk/attack/pain and are
            /// mandatory for attaching; the rest are the death sequence plus
            /// the corpse and are optional (all-or-nothing as a group).
            public readonly int LiveFrameCount;
            // TRELLIS front view looks down -Z after Unity import; calibrated
            // per set at the import gate.
            public readonly float YawOffsetDeg;

            public MonsterModelSet(string sprite, string[] lumps,
                                   float[] heightsPx, int liveFrameCount,
                                   float yawOffsetDeg)
            {
                Sprite = sprite;
                FrameLumps = lumps;
                PatchHeightsPx = heightsPx;
                LiveFrameCount = liveFrameCount;
                YawOffsetDeg = yawOffsetDeg;
            }
        }

        // Native patch heights (px) mirror the WAD patch headers per frame —
        // the billboard renders each frame at patch size, so per-frame mesh
        // normalization keeps the exact same silhouette scale behaviour. The
        // death tail shrinks as the body collapses, so those meshes must be
        // modelled lying down or the height-driven scale blows them up.
        //
        // Death coverage stops at the last frame that still reads as a BODY:
        // Freedoom kills dissolve into gore (POSS loses its head on H0, SARG
        // is a puddle of chunks by K0, BOSS a heap of bone), and a 19-30 px
        // flat splatter has no volume for TRELLIS to reconstruct. Coverage is
        // therefore a contiguous PREFIX of the death sequence; the remaining
        // frames and the corpse stay on the billboard, exactly as the whole
        // death did before this stage.
        static readonly Dictionary<string, MonsterModelSet> Sets = new()
        {
            // Death: H0 (hit, head bursting) and I0 (mid-collapse) still read
            // as a zombie; J0 onward is a pile of boots.
            ["POSS"] = new MonsterModelSet(
                "POSS",
                new[] { "A1", "B1", "C1", "D1", "E1", "F1", "G1",
                        "H0", "I0" },
                new[] { 57f, 57f, 57f, 57f, 56f, 56f, 55f,
                        55f, 42f },
                liveFrameCount: 7,
                yawOffsetDeg: 0f),
            // Attaches only once all 7 live frame meshes land in Resources
            // (TryAttach is all-or-nothing), so listing ahead is safe. The
            // sergeant has the cleanest fall of the roster — four death
            // frames keep a body before L0 flattens.
            ["SPOS"] = new MonsterModelSet(
                "SPOS",
                new[] { "A1", "B1", "C1", "D1", "E1", "F1", "G1",
                        "H0", "I0", "J0", "K0" },
                new[] { 55f, 55f, 56f, 56f, 56f, 56f, 55f,
                        60f, 53f, 39f, 34f },
                liveFrameCount: 7,
                yawOffsetDeg: 0f),
            // Demon: melee attack spans E-F-G, pain is H (8 live frames).
            // The spectre (58) never routes here — ThingSpawner keeps it on
            // the MF_SHADOW billboard.
            // Death: the demon bursts — I0 and J0 are the last frames with a
            // body, K0 onward is spraying gore.
            ["SARG"] = new MonsterModelSet(
                "SARG",
                new[] { "A1", "B1", "C1", "D1", "E1", "F1", "G1", "H1",
                        "I0", "J0" },
                new[] { 59f, 59f, 59f, 59f, 60f, 60f, 60f, 50f,
                        59f, 60f },
                liveFrameCount: 8,
                yawOffsetDeg: 0f),
            // Imp: attack spans E-F-G (fireball launches on G), pain is H.
            // Offset stays 0 like every monster (all TRELLIS meshes share
            // the same forward): the 2026-08-14 «walks back-first» reports
            // were the FACE being unreadable (eyes lost to quantization) —
            // fixed by the eye-boost in project_hint_texture, not by yaw.
            // The imp holds its shape longest of the roster: I0-L0 are a
            // twisting collapse, M0 is the flat heap.
            ["TROO"] = new MonsterModelSet(
                "TROO",
                new[] { "A1", "B1", "C1", "D1", "E1", "F1", "G1", "H1",
                        "I0", "J0", "K0", "L0" },
                new[] { 60f, 62f, 60f, 62f, 62f, 61f, 64f, 63f,
                        63f, 62f, 54f, 43f },
                liveFrameCount: 8,
                yawOffsetDeg: 0f),
            // Baron of Hell (E1M8 finale): attack E-F-G, pain H. Death I0-J0
            // is the standing hit and the buckle; from K0 the baron is a heap
            // of bone and meat.
            ["BOSS"] = new MonsterModelSet(
                "BOSS",
                new[] { "A1", "B1", "C1", "D1", "E1", "F1", "G1", "H1",
                        "I0", "J0" },
                new[] { 69f, 72f, 69f, 72f, 74f, 73f, 74f, 73f,
                        73f, 69f },
                liveFrameCount: 8,
                yawOffsetDeg: 0f),
        };

        MonsterModelSet set;
        SpriteBillboard billboard;
        MeshRenderer billboardRenderer;
        Transform yawPivot;
        GameObject[] framePrefabs;
        Texture2D[] frameEmission;
        GameObject[] frameModels;
        float worldScale;
        bool deathCovered;
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
        public bool DeathCoveredForTest => deathCovered;

        /// Attach when every live frame of the sprite has an accepted mesh —
        /// all-or-nothing, same rule as the SpriteCache animation gate for
        /// display redraws (partial coverage would flicker between styles).
        /// The death tail is a second all-or-nothing group: complete → the
        /// kill stays 3D, incomplete → death hands over to the billboard.
        public static ExperimentalMonsterModel TryAttach(
            GameObject monsterRoot,
            string sprite,
            float worldScale,
            SpriteBillboard billboard)
        {
            if (monsterRoot == null || sprite == null) return null;
            if (!Sets.TryGetValue(sprite, out var set)) return null;

            var prefabs = new GameObject[set.FrameLumps.Length];
            var emissionMasks = new Texture2D[set.FrameLumps.Length];
            bool liveMasks = true;
            bool deathCovered = true;
            for (int i = 0; i < set.FrameLumps.Length; i++)
            {
                bool live = i < set.LiveFrameCount;
                string resource =
                    ResourceRoot + set.Sprite + "/" + set.Sprite + set.FrameLumps[i];
                prefabs[i] = Resources.Load<GameObject>(resource);
                if (prefabs[i] == null)
                {
                    if (live) return null;
                    deathCovered = false;
                    continue;
                }
                // Optional steady-glow masks (SPOS visor). All-or-nothing like
                // the frame meshes: a partial set would strobe during walk.
                emissionMasks[i] = Resources.Load<Texture2D>(resource + "_emission");
                if (live && emissionMasks[i] == null) liveMasks = false;
            }
            if (!liveMasks)
                emissionMasks = null;

            var model = monsterRoot.AddComponent<ExperimentalMonsterModel>();
            model.Init(set, prefabs, emissionMasks, deathCovered, worldScale,
                       billboard);
            if (!model.HasModel)
            {
                Destroy(model);
                return null;
            }
            return model;
        }

        void Init(MonsterModelSet set, GameObject[] prefabs,
                  Texture2D[] emissionMasks, bool deathFramesCovered,
                  float worldScaleUnits, SpriteBillboard sourceBillboard)
        {
            this.set = set;
            framePrefabs = prefabs;
            frameEmission = emissionMasks;
            deathCovered = deathFramesCovered;
            worldScale = worldScaleUnits;
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

            // Only the live frames are instantiated up front. Death meshes
            // are needed once, by the monsters that actually die, so they
            // are built on demand (a level's whole population would otherwise
            // carry ~1.7× the GameObjects for frames most of them never show).
            frameModels = new GameObject[prefabs.Length];
            for (int i = 0; i < set.LiveFrameCount; i++)
            {
                if (!EnsureFrameInstance(i))
                {
                    Destroy(pivotGo);
                    yawPivot = null;
                    return;
                }
            }

            currentFrame = 0;
            currPos = prevPos = transform.position;
            currAngleDeg = prevAngleDeg =
                billboard != null ? billboard.DoomAngleDegrees : 0f;
            poseSeeded = true;

            SettingsController.SettingsApplied += OnSettingsApplied;
            RefreshVisibility(force: true);
        }

        /// Build one frame's mesh instance (inactive). Returns false when the
        /// prefab is missing or has no renderers.
        bool EnsureFrameInstance(int index)
        {
            if (frameModels == null || yawPivot == null) return false;
            if (frameModels[index] != null) return true;
            if (framePrefabs[index] == null) return false;

            var instance = Instantiate(framePrefabs[index], yawPivot);
            instance.name = set.Sprite + set.FrameLumps[index];
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            var renderers = instance.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning(
                    $"ExperimentalMonsterModel: '{instance.name}' has no renderers.");
                Destroy(instance);
                return false;
            }
            NormalizeFrame(instance, renderers,
                           set.PatchHeightsPx[index] * worldScale);
            ConfigureMaterials(renderers,
                               frameEmission != null ? frameEmission[index] : null);
            instance.SetActive(false);
            frameModels[index] = instance;
            return true;
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

        /// Brain frame switch. Frames inside the set select the matching mesh
        /// (death frames build their instance on first use); any frame outside
        /// coverage — an uncovered death tail, or xdeath gibs — drops to the
        /// billboard for good.
        public void NotifyFrame(int frame)
        {
            if (reverted || frameModels == null) return;
            if (frame < 0 || frame >= frameModels.Length ||
                (frame >= set.LiveFrameCount && !deathCovered))
            {
                RevertToBillboard();
                return;
            }
            if (frame == currentFrame) return;
            if (!EnsureFrameInstance(frame))
            {
                RevertToBillboard();
                return;
            }
            if (ModelVisible)
            {
                if (frameModels[currentFrame] != null)
                    frameModels[currentFrame].SetActive(false);
                frameModels[frame].SetActive(true);
            }
            currentFrame = frame;
        }

        /// The monster just started dying. A covered death tail keeps the mesh
        /// (the frames arrive through NotifyFrame); gibs and monsters without
        /// death meshes hand over to the billboard before the first fall frame.
        public void NotifyDeathStarted(bool extremeDeath)
        {
            if (reverted) return;
            if (extremeDeath || !deathCovered)
                RevertToBillboard();
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

        /// Gibs / uncovered death / restore of a corpse outside coverage: hide
        /// the mesh forever and hand presentation back to the billboard
        /// (barrel BEXP pattern).
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
                        if (frameModels[i] != null)
                            frameModels[i].SetActive(i == currentFrame);
            }
            if (billboardRenderer != null)
                billboardRenderer.enabled = !useMesh;
            if (billboard != null)
                billboard.enabled = !useMesh;
        }

        /// Test seam: the frame table of a routed sprite. Frame index is the
        /// brain's frame number, so the table must line up with MonsterTable's
        /// death sequence and ThingTable's corpse frame.
        public static bool TryGetFrameTableForTest(
            string sprite, out int liveFrameCount,
            out string[] frameLumps, out float[] patchHeightsPx)
        {
            if (sprite != null && Sets.TryGetValue(sprite, out var s))
            {
                liveFrameCount = s.LiveFrameCount;
                frameLumps = (string[])s.FrameLumps.Clone();
                patchHeightsPx = (float[])s.PatchHeightsPx.Clone();
                return true;
            }
            liveFrameCount = 0;
            frameLumps = null;
            patchHeightsPx = null;
            return false;
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
