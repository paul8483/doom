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
    /// thing root; Classic keeps the sprite billboard.
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
            /// Lump -> native patch WIDTH (px) for the frames that lie flat
            /// on the floor. A pile's own thickness IS its pose, so scaling
            /// it to the patch HEIGHT stands it back up (see FlatWidthPx).
            public readonly Dictionary<string, float> FlatWidthsPx;
            /// Frames [0, LiveFrameCount) are stand/walk/attack/pain and are
            /// mandatory for attaching; the rest are the death sequence plus
            /// the corpse, covered as far as their meshes exist.
            public readonly int LiveFrameCount;
            // TRELLIS front view looks down -Z after Unity import; calibrated
            // per set at the import gate.
            public readonly float YawOffsetDeg;
            /// Final XDEATH gib-corpse lump ("U0") and its patch WIDTH: the
            /// gib ANIMATION is a spray of loose pixels and stays on the
            /// billboard (fireball finding), but the lasting pool of remains
            /// is a body again and gets a mesh. Null = no xdeath corpse mesh.
            public readonly string XdeathLump;
            public readonly float XdeathWidthPx;
            /// Brain frame index of the xdeath corpse (frame letter - 'A').
            public int XdeathFrameIndex =>
                XdeathLump == null ? -1 : XdeathLump[0] - 'A';
            /// Fire frame that shows a shader-drawn muzzle flash (the mesh
            /// cannot carry one: a baked fire stop-frame is a lump of
            /// geometry — the SPOSF1 black star, 2026-08-27). -1 = none.
            /// Position is in the frame mesh's own space (the flash quad is
            /// a child of the frame instance, so normalization carries it);
            /// size is the quad side in the same units (mesh height = 1).
            public readonly int MuzzleFlashFrame = -1;
            public readonly Vector3 MuzzleFlashLocalPos;
            public readonly float MuzzleFlashSize;

            public MonsterModelSet(string sprite, string[] lumps,
                                   float[] heightsPx, int liveFrameCount,
                                   float yawOffsetDeg,
                                   (string Lump, float WidthPx)[] flatFrames = null,
                                   (string Lump, float WidthPx)? xdeathCorpse = null,
                                   (int Frame, Vector3 LocalPos, float Size)? muzzleFlash = null)
            {
                if (muzzleFlash.HasValue)
                {
                    MuzzleFlashFrame = muzzleFlash.Value.Frame;
                    MuzzleFlashLocalPos = muzzleFlash.Value.LocalPos;
                    MuzzleFlashSize = muzzleFlash.Value.Size;
                }
                Sprite = sprite;
                FrameLumps = lumps;
                PatchHeightsPx = heightsPx;
                LiveFrameCount = liveFrameCount;
                YawOffsetDeg = yawOffsetDeg;
                FlatWidthsPx = new Dictionary<string, float>();
                if (flatFrames != null)
                    foreach (var (lump, widthPx) in flatFrames)
                        FlatWidthsPx[lump] = widthPx;
                if (xdeathCorpse.HasValue)
                {
                    XdeathLump = xdeathCorpse.Value.Lump;
                    XdeathWidthPx = xdeathCorpse.Value.WidthPx;
                }
            }

            /// > 0 when the frame is a pile that lies flat and must be scaled
            /// by width instead of height.
            public float FlatWidthPx(int index) =>
                FlatWidthsPx.TryGetValue(FrameLumps[index], out float w) ? w : 0f;
        }

        // Native patch heights (px) mirror the WAD patch headers per frame —
        // the billboard renders each frame at patch size, so per-frame mesh
        // normalization keeps the exact same silhouette scale behaviour. The
        // death tail shrinks as the body collapses, so those meshes must be
        // modelled lying down or the height-driven scale blows them up.
        //
        // The table declares the WHOLE death chain including the corpse
        // frame; TryAttach covers it as far as the meshes on disk reach, so a
        // monster can be filled in one frame at a time without its finished
        // frames regressing to the billboard.
        static readonly Dictionary<string, MonsterModelSet> Sets = new()
        {
            // Death H0-K0 plus the L0 corpse: the head bursts on H0 and the
            // body is a pile of boots by J0, so the late frames lean on the
            // lay-down bake (see Tools/lay_down_mesh.py).
            ["POSS"] = new MonsterModelSet(
                "POSS",
                new[] { "A1", "B1", "C1", "D1", "E1", "F1", "G1",
                        "H0", "I0", "J0", "K0", "L0" },
                new[] { 57f, 57f, 57f, 57f, 56f, 56f, 55f,
                        55f, 42f, 34f, 27f, 19f },
                liveFrameCount: 7,
                yawOffsetDeg: 0f,
                // Chain re-rolled 2026-08-22 on one consistent hint set (live
                // clothing, chest wound, one continuous fall). K0 is the
                // mid-roll onto the side and L0 the face-down corpse — both
                // lying slabs, so they take the patch WIDTH; H0 stands and
                // I0/J0 are pitched to the native aspect (90/40 deg), all
                // three on the height rule.
                flatFrames: new[] { ("K0", 48f), ("L0", 50f) },
                xdeathCorpse: ("U0", 67f),
                // Pistol fire frame F1: the mesh already aims down +Z, so no
                // yaw bake — the flash sits at the measured pistol tip
                // (max-Z cluster). Slightly smaller than the shotgun burst.
                muzzleFlash: (5, new Vector3(0f, 0.34f, 0.42f), 0.40f)),
            // Attaches only once all 7 live frame meshes land in Resources
            // (live coverage is all-or-nothing), so listing ahead is safe.
            // H0-K0 are the accepted fall (Gate D1); L0 is the corpse heap.
            ["SPOS"] = new MonsterModelSet(
                "SPOS",
                new[] { "A1", "B1", "C1", "D1", "E1", "F1", "G1",
                        "H0", "I0", "J0", "K0", "L0" },
                new[] { 55f, 55f, 56f, 56f, 56f, 56f, 55f,
                        60f, 53f, 39f, 34f, 20f },
                liveFrameCount: 7,
                yawOffsetDeg: 0f,
                xdeathCorpse: ("U0", 67f),
                // Fire frame F1 has the -40deg yaw baked in (muzzle at the
                // target) and shows the shader flash at the measured muzzle
                // tip. The burst texture's hot core spans ~43% of the quad
                // (streaks and sparks fill the rest), so 0.45 keeps the ball
                // itself near the native flash scale (0.135 of patch height,
                // boosted for game-distance readability).
                muzzleFlash: (5, new Vector3(0f, 0.076f, 0.5f), 0.45f)),
            // Demon: melee attack spans E-F-G, pain is H (8 live frames).
            // The spectre (58) shares this whole set — same meshes, same
            // chain — with the ghost material swapped in (spectre flag).
            // Death: the demon bursts — I0/J0 still have a body, K0-M0 are
            // spraying gore and N0 is the corpse pool.
            ["SARG"] = new MonsterModelSet(
                "SARG",
                new[] { "A1", "B1", "C1", "D1", "E1", "F1", "G1", "H1",
                        "I0", "J0", "K0", "L0", "M0", "N0" },
                new[] { 59f, 59f, 59f, 59f, 60f, 60f, 60f, 50f,
                        59f, 60f, 53f, 40f, 30f, 29f },
                liveFrameCount: 8,
                yawOffsetDeg: 0f,
                // K0-N0 are gore on the floor: the meshes are flat slabs, so
                // they take the patch WIDTH and keep their own thickness.
                flatFrames: new[] { ("K0", 43f), ("L0", 43f),
                                    ("M0", 48f), ("N0", 49f) }),
            // Imp: attack spans E-F-G (fireball launches on G), pain is H.
            // Offset stays 0 like every monster (all TRELLIS meshes share
            // the same forward): the 2026-08-14 «walks back-first» reports
            // were the FACE being unreadable (eyes lost to quantization) —
            // fixed by the eye-boost in project_hint_texture, not by yaw.
            // The imp holds its shape longest of the roster: I0-L0 are a
            // twisting collapse, M0 is the corpse heap.
            ["TROO"] = new MonsterModelSet(
                "TROO",
                new[] { "A1", "B1", "C1", "D1", "E1", "F1", "G1", "H1",
                        "I0", "J0", "K0", "L0", "M0" },
                new[] { 60f, 62f, 60f, 62f, 62f, 61f, 64f, 63f,
                        63f, 62f, 54f, 43f, 26f },
                liveFrameCount: 8,
                yawOffsetDeg: 0f,
                // K0/L0 are the imp already down on the floor. M0 is not
                // listed: that corpse was modelled as a mound and squashed to
                // the sprite's aspect, so the height rule lands it correctly.
                flatFrames: new[] { ("K0", 45f), ("L0", 42f) },
                xdeathCorpse: ("U0", 66f)),
            // Baron of Hell (E1M8 finale): attack E-F-G, pain H. Death I0-J0
            // is the standing hit and the buckle; from K0 the baron collapses
            // into a heap of bone and meat, and O0 is both the last death
            // frame and the corpse.
            ["BOSS"] = new MonsterModelSet(
                "BOSS",
                new[] { "A1", "B1", "C1", "D1", "E1", "F1", "G1", "H1",
                        "I0", "J0", "K0", "L0", "M0", "N0", "O0" },
                new[] { 69f, 72f, 69f, 72f, 74f, 73f, 74f, 73f,
                        73f, 69f, 67f, 52f, 46f, 38f, 24f },
                liveFrameCount: 8,
                yawOffsetDeg: 0f,
                // The baron spreads WIDE as it falls (63 -> 90 px of patch
                // width while the height drops 73 -> 24). L0 onward came back
                // from TRELLIS already flat — thickness 0.12-0.31 against a
                // width of 1.0 — so they take the patch width. K0 still has
                // real volume (0.68) and stays on the height rule.
                flatFrames: new[] { ("L0", 80f), ("M0", 82f),
                                    ("N0", 82f), ("O0", 90f) }),
        };

        /// Maps map-placed dead-monster decorations (info.c MT_DEAD*) onto the
        /// same corpse meshes the death chains end on, with the same
        /// normalization rule. Vanilla draws these things with the corpse
        /// sprite; without this seam they were the last billboards left lying
        /// among 3D corpses (E1M1 alone places nine of them).
        public static bool TryDescribeCorpse(
            int doomedNum, out string resource, out float sizePx,
            out bool byWidth, out string emissionResource)
        {
            // Map-placed gib pools (10 "bloody mess", 12 — both draw the
            // gibbed player) reuse the PLAYW0 xdeath-corpse mesh. There is
            // no PLAY monster set, so they are described directly.
            if (doomedNum == 10 || doomedNum == 12)
            {
                resource = "ExperimentalMonsters/PLAY/PLAYW0";
                emissionResource = null;
                sizePx = 53f; // PLAYW0 patch width
                byWidth = true;
                return true;
            }

            // Dead player (15) draws the PLAYN0 corpse sprite.
            if (doomedNum == 15)
            {
                resource = "ExperimentalMonsters/PLAY/PLAYN0";
                emissionResource = null;
                sizePx = 37f; // PLAYN0 patch width
                byWidth = true;
                return true;
            }

            string sprite = doomedNum switch
            {
                18 => "POSS",
                19 => "SPOS",
                20 => "TROO",
                21 => "SARG",
                _ => null,
            };
            if (sprite == null || !Sets.TryGetValue(sprite, out var set))
            {
                resource = null;
                emissionResource = null;
                sizePx = 0f;
                byWidth = false;
                return false;
            }

            int last = set.FrameLumps.Length - 1;
            string lump = sprite + set.FrameLumps[last];
            float flatWidth = set.FlatWidthPx(last);
            byWidth = flatWidth > 0f;
            sizePx = byWidth ? flatWidth : set.PatchHeightsPx[last];
            resource = "ExperimentalMonsters/" + sprite + "/" + lump;
            emissionResource = resource + "_emission";
            return true;
        }

        MonsterModelSet set;
        SpriteBillboard billboard;
        MeshRenderer billboardRenderer;
        Transform yawPivot;
        GameObject[] framePrefabs;
        Texture2D[] frameEmission;
        GameObject[] frameModels;
        float worldScale;
        /// How many death frames after LiveFrameCount have meshes on disk —
        /// presentation hands over to the billboard at the first frame past it.
        int coveredDeathFrames;
        /// MF_SHADOW thing (58): frame meshes take the translucent ghost
        /// shader instead of the opaque unlit one.
        bool spectre;
        readonly List<Material> ownedMaterials = new List<Material>();

        int currentFrame;
        bool reverted;
        bool lastUseMesh;
        bool settingsControllerSeen;
        // XDEATH: the gib ANIMATION rides the billboard (a spray of loose
        // pixels has no body to model), then the lasting gib-corpse frame
        // swaps in its own mesh. gibInterlude spans the animation.
        GameObject xdeathPrefab;
        GameObject xdeathInstance;
        bool gibInterlude;
        bool xdeathShown;

        // Rest offset: a lying corpse mesh is a slab of ~50 DOOM units, while
        // the thing is a point. Killed with its centre on a sector edge (the
        // E1M3 lift 47 sergeant, 2026-09-02) the slab hangs half over the
        // neighbour and gets sliced once that floor moves. The pivot — never
        // the thing — slides inside its own sector across the death chain
        // (vanilla corpses slide on momentum too); the gameplay origin,
        // collision and save identity stay put. Resolved once per death from
        // the final covered frame's footprint; a restored corpse snaps.
        const float RestSlideRate = 4f; // fraction per second
        Vector3 restOffset;
        bool restOffsetResolved;
        float restFractionTarget;
        float restFractionShown;

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
        public bool DeathCoveredForTest => coveredDeathFrames > 0;
        public int CoveredDeathFramesForTest => coveredDeathFrames;
        public bool SpectreForTest => spectre;
        /// World-space XZ shift the pivot takes at rest (zero until death).
        public Vector3 RestOffsetForTest => restOffset;
        public float RestFractionForTest => restFractionShown;

        /// Test seam: pretend only this many death meshes are on disk. Every
        /// routed monster now ships its whole chain, so the "death tail is
        /// uncovered → hand over to the billboard for good" path — the one a
        /// monster takes while its meshes are still being authored — has no
        /// live asset gap left to exercise it.
        public static int DeathCoverageCapForTest = int.MaxValue;

        /// Attach when every live frame of the sprite has an accepted mesh —
        /// all-or-nothing, same rule as the SpriteCache animation gate for
        /// display redraws (partial coverage would flicker between styles).
        /// The death tail is a second all-or-nothing group: complete → the
        /// kill stays 3D, incomplete → death hands over to the billboard.
        public static ExperimentalMonsterModel TryAttach(
            GameObject monsterRoot,
            string sprite,
            float worldScale,
            SpriteBillboard billboard,
            bool spectre = false)
        {
            if (monsterRoot == null || sprite == null) return null;
            if (!Sets.TryGetValue(sprite, out var set)) return null;

            var prefabs = new GameObject[set.FrameLumps.Length];
            var emissionMasks = new Texture2D[set.FrameLumps.Length];
            bool liveMasks = true;
            // The death chain is covered up to its first missing mesh: the
            // table declares the whole sequence, the assets on disk decide how
            // far 3D reaches. A hole mid-chain would flip styles twice, so
            // everything past the first gap stays on the billboard.
            int coveredDeathFrames = 0;
            bool deathChainOpen = true;
            for (int i = 0; i < set.FrameLumps.Length; i++)
            {
                bool live = i < set.LiveFrameCount;
                string resource =
                    ResourceRoot + set.Sprite + "/" + set.Sprite + set.FrameLumps[i];
                prefabs[i] = Resources.Load<GameObject>(resource);
                if (prefabs[i] == null)
                {
                    if (live) return null;
                    deathChainOpen = false;
                    continue;
                }
                if (!live && deathChainOpen) coveredDeathFrames++;
                // Optional steady-glow masks (SPOS visor). All-or-nothing like
                // the frame meshes: a partial set would strobe during walk.
                emissionMasks[i] = Resources.Load<Texture2D>(resource + "_emission");
                if (live && emissionMasks[i] == null) liveMasks = false;
            }
            if (!liveMasks)
                emissionMasks = null;
            if (coveredDeathFrames > DeathCoverageCapForTest)
                coveredDeathFrames = DeathCoverageCapForTest;

            var model = monsterRoot.AddComponent<ExperimentalMonsterModel>();
            if (set.XdeathLump != null)
                model.xdeathPrefab = Resources.Load<GameObject>(
                    ResourceRoot + set.Sprite + "/" + set.Sprite + set.XdeathLump);
            model.Init(set, prefabs, emissionMasks, coveredDeathFrames, worldScale,
                       billboard, spectre);
            if (!model.HasModel)
            {
                Destroy(model);
                return null;
            }
            return model;
        }

        void Init(MonsterModelSet set, GameObject[] prefabs,
                  Texture2D[] emissionMasks, int deathFramesCovered,
                  float worldScaleUnits, SpriteBillboard sourceBillboard,
                  bool asSpectre)
        {
            this.set = set;
            spectre = asSpectre;
            framePrefabs = prefabs;
            frameEmission = emissionMasks;
            coveredDeathFrames = deathFramesCovered;
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
            float flatWidthPx = set.FlatWidthPx(index);
            NormalizeFrame(instance, renderers,
                           (flatWidthPx > 0f ? flatWidthPx
                                             : set.PatchHeightsPx[index])
                           * worldScale,
                           byWidth: flatWidthPx > 0f);
            ConfigureMaterials(renderers,
                               frameEmission != null ? frameEmission[index] : null);
            if (index == set.MuzzleFlashFrame)
                AttachMuzzleFlash(instance);
            instance.SetActive(false);
            frameModels[index] = instance;
            return true;
        }

        /// Shader-drawn muzzle flash on the fire frame: a small camera-facing
        /// quad at the muzzle tip, colored by a radial LUT baked from the
        /// native sprite's own flash texels (fireball pattern on a disc). The
        /// quad is a child of the frame instance, so it appears and vanishes
        /// with the frame's own SetActive — vanilla cadence for free.
        void AttachMuzzleFlash(GameObject instance)
        {
            Shader shader = Resources.Load<Shader>(
                "ExperimentalMonsters/DoomExperimentalMuzzleFlash");
            var lut = Resources.Load<Texture2D>(
                ResourceRoot + set.Sprite + "/" + set.Sprite +
                set.FrameLumps[set.MuzzleFlashFrame] + "_flash");
            if (shader == null || lut == null) return;

            var go = new GameObject("MuzzleFlash");
            go.transform.SetParent(instance.transform, worldPositionStays: false);
            go.transform.localPosition = set.MuzzleFlashLocalPos;
            go.transform.localScale = Vector3.one * set.MuzzleFlashSize;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = SharedFlashQuad();
            var renderer = go.AddComponent<MeshRenderer>();
            var material = new Material(shader) { mainTexture = lut };
            renderer.sharedMaterial = material;
            ownedMaterials.Add(material);
            go.AddComponent<MuzzleFlashFacing>();
        }

        /// One shared unit quad for every flash in the app: instances scale
        /// it via their transform, and sharing sidesteps per-quad mesh
        /// destruction bookkeeping (the Stage 6c billboard-leak lesson).
        static Mesh sharedFlashQuad;
        static Mesh SharedFlashQuad()
        {
            if (sharedFlashQuad != null) return sharedFlashQuad;
            var mesh = new Mesh { name = "MuzzleFlashQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
            sharedFlashQuad = mesh;
            return mesh;
        }

        /// Full billboard: the flash disc copies the camera's rotation each
        /// frame (a flash is round from every angle). Shader culls off, so
        /// the copied rotation only has to keep the quad screen-aligned.
        sealed class MuzzleFlashFacing : MonoBehaviour
        {
            Camera cachedCamera;

            void LateUpdate()
            {
                if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
                    cachedCamera = Camera.main;
                if (cachedCamera != null)
                    transform.rotation = cachedCamera.transform.rotation;
            }
        }

        /// Scale the frame to its native patch size and anchor it at the
        /// pivot origin — bottom on the floor, bounds-centered on XZ (the same
        /// anchor the billboard quad uses for floor things).
        ///
        /// Standing frames scale by HEIGHT: the sprite is a figure on its feet
        /// and its patch height is that figure's height. Frames that lie flat
        /// scale by WIDTH instead, because a pile's Y extent is its thickness,
        /// not its size — matching it to the patch height stretches the pile
        /// into a vertical mass that reads as propped against a wall rather
        /// than lying on the floor (the 2026-08-17 gate). Their screen height
        /// then comes from the mesh's own depth, as it does for a real object.
        void NormalizeFrame(GameObject instance, Renderer[] renderers,
                            float target, bool byWidth)
        {
            Bounds bounds = PivotBounds(instance, renderers);
            float span = byWidth ? bounds.size.x : bounds.size.y;
            if (span <= 0.0001f) return;

            instance.transform.localScale *= target / span;
            bounds = PivotBounds(instance, renderers);

            Vector3 local = instance.transform.localPosition;
            instance.transform.localPosition = new Vector3(
                local.x - bounds.center.x,
                local.y - bounds.min.y,
                local.z - bounds.center.z);
        }

        /// Combined bounds in the yaw pivot's own space. Renderer.bounds is a
        /// world AABB, which for the width measurement would ride the monster's
        /// facing — a frame instantiated while the corpse faces sideways would
        /// scale by its depth. Death frames are built lazily, so that is the
        /// normal case, not an edge one.
        Bounds PivotBounds(GameObject instance, Renderer[] renderers)
        {
            Matrix4x4 toPivot = yawPivot.worldToLocalMatrix;
            var bounds = new Bounds();
            bool started = false;
            foreach (var r in renderers)
            {
                Mesh mesh = (r as MeshRenderer) != null
                    ? r.GetComponent<MeshFilter>()?.sharedMesh
                    : null;
                if (mesh == null) continue;
                Matrix4x4 m = toPivot * r.transform.localToWorldMatrix;
                Bounds local = mesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = local.center + Vector3.Scale(
                        local.extents,
                        new Vector3((c & 1) == 0 ? -1f : 1f,
                                    (c & 2) == 0 ? -1f : 1f,
                                    (c & 4) == 0 ? -1f : 1f));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!started) { bounds = new Bounds(p, Vector3.zero); started = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        /// Same presentation stack as pickups/decorations: Resources-loaded
        /// unlit shader (exposure 1.0, SectorFog aware). An optional emission
        /// mask lights masked texels steadily in the albedo's own hue (blue
        /// visor) — discrete-blink path with _Blink pinned to 1. The spectre
        /// takes the translucent ghost shader instead (MF_SHADOW analog:
        /// depth-primed single-layer blend + UV shimmer, no emission).
        void ConfigureMaterials(Renderer[] renderers, Texture2D emissionMask)
        {
            Shader shader = Resources.Load<Shader>(
                spectre ? "ExperimentalMonsters/DoomExperimentalSpectre"
                        : "ExperimentalPickups/DoomExperimentalPickupUnlit");
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
                    if (!spectre && emissionMask != null)
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
            // The lasting xdeath gib-corpse frame swaps in its own mesh; the
            // gib animation frames before it ride the billboard untouched.
            if (xdeathPrefab != null && frame == set.XdeathFrameIndex)
            {
                ShowXdeathCorpse();
                return;
            }
            if (gibInterlude) return;
            if (frame < 0 || frame >= frameModels.Length ||
                frame >= set.LiveFrameCount + coveredDeathFrames)
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
            if (frame >= set.LiveFrameCount)
                AdvanceRestOffset(frame);
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
            if (extremeDeath && xdeathPrefab != null)
            {
                // Billboard interlude: the gib spray plays as the native
                // sprite, then NotifyFrame(U) swaps in the gib-corpse mesh.
                gibInterlude = true;
                if (yawPivot != null)
                    yawPivot.gameObject.SetActive(false);
                if (billboardRenderer != null)
                    billboardRenderer.enabled = true;
                if (billboard != null)
                    billboard.enabled = true;
                return;
            }
            if (extremeDeath || coveredDeathFrames == 0)
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
            if (!ModelVisible)
            {
                // A corpse that died on the billboard side of a hot-switch
                // must not slide when the mesh comes back.
                restFractionShown = restFractionTarget;
                return;
            }

            restFractionShown = Mathf.MoveTowards(
                restFractionShown, restFractionTarget, Time.deltaTime * RestSlideRate);
            poseAlpha = Mathf.Clamp01(poseAlpha + Time.deltaTime * PoseInterpRate);
            float renderAngle = Mathf.LerpAngle(prevAngleDeg, currAngleDeg, poseAlpha);
            Vector3 visualPos = Vector3.Lerp(prevPos, currPos, poseAlpha);

            // DOOM angle is CCW from East; Unity yaw is CW from +Z (North).
            yawPivot.rotation =
                Quaternion.Euler(0f, 90f - renderAngle + set.YawOffsetDeg, 0f);
            yawPivot.position = transform.position + (visualPos - currPos)
                                + restOffset * restFractionShown;
        }

        // ── Rest offset (corpse footprint inside its own sector) ─────────────

        /// Death frame `frame` arrived: resolve the slide once, then move the
        /// target fraction along the chain so the body settles fully inside
        /// its sector on the corpse frame. The first frame seen being the
        /// corpse itself (save restore, or a chain of one) snaps — there is
        /// no fall to slide through.
        void AdvanceRestOffset(int frame)
        {
            bool first = !restOffsetResolved;
            if (first)
            {
                int last = set.LiveFrameCount + coveredDeathFrames - 1;
                if (last < frame) last = frame;
                if (EnsureFrameInstance(last))
                    ResolveRestOffset(frameModels[last]);
                else
                    restOffsetResolved = true;
            }
            int chain = set.FrameLumps.Length - set.LiveFrameCount;
            float target = chain > 0
                ? Mathf.Clamp01((frame - set.LiveFrameCount + 1) / (float)chain)
                : 1f;
            if (target > restFractionTarget) restFractionTarget = target;
            if (first && target >= 1f) restFractionShown = 1f;
        }

        /// Measure the (normalised) frame's footprint in pivot space and ask
        /// the map for the shift that keeps it inside the sector under the
        /// thing. Facing is read from the billboard: it carries the restored
        /// angle of a saved corpse, which never went through a gameplay pose.
        void ResolveRestOffset(GameObject instance)
        {
            restOffsetResolved = true;
            restOffset = Vector3.zero;
            if (instance == null || yawPivot == null) return;
            var renderers = instance.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0) return;

            if (billboard != null)
                currAngleDeg = prevAngleDeg = billboard.DoomAngleDegrees;
            float yaw = 90f - currAngleDeg + set.YawOffsetDeg;
            Bounds bounds = PivotBounds(instance, renderers);
            restOffset = CorpseFootprintClamp.Resolve(
                transform.position, yaw,
                bounds.extents.x, bounds.extents.z, worldScale);
        }

        /// The gib-corpse mesh, built on first use (a level's population
        /// rarely gets gibbed). Failure falls back to the billboard for good.
        void ShowXdeathCorpse()
        {
            if (xdeathShown) return;
            if (xdeathInstance == null)
            {
                var instance = Instantiate(xdeathPrefab, yawPivot);
                instance.name = set.Sprite + set.XdeathLump;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                var renderers = instance.GetComponentsInChildren<Renderer>(includeInactive: true);
                if (renderers.Length == 0)
                {
                    Destroy(instance);
                    RevertToBillboard();
                    return;
                }
                NormalizeFrame(instance, renderers,
                               set.XdeathWidthPx * worldScale, byWidth: true);
                ConfigureMaterials(renderers, null);
                xdeathInstance = instance;
            }
            // The gib spray hid the body, so the pool appears already in place.
            ResolveRestOffset(xdeathInstance);
            restFractionTarget = restFractionShown = 1f;
            xdeathShown = true;
            gibInterlude = false;
            if (frameModels != null)
                foreach (var m in frameModels)
                    if (m != null) m.SetActive(false);
            xdeathInstance.SetActive(true);
            RefreshVisibility(force: true);
        }

        public bool XdeathCorpseShownForTest => xdeathShown;
        public bool GibInterludeForTest => gibInterlude;

        void RefreshVisibility(bool force)
        {
            if (reverted) return;

            bool useMesh = ResolveUseMesh() && !gibInterlude;
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
            if (yawPivot != null)
            {
                yawPivot.gameObject.SetActive(useMesh);
                if (useMesh && frameModels != null)
                    for (int i = 0; i < frameModels.Length; i++)
                        if (frameModels[i] != null)
                            frameModels[i].SetActive(!xdeathShown && i == currentFrame);
                if (xdeathInstance != null)
                    xdeathInstance.SetActive(useMesh && xdeathShown);
            }
            if (billboardRenderer != null)
                billboardRenderer.enabled = !useMesh;
            if (billboard != null)
                billboard.enabled = !useMesh;
        }

        /// Test seam: the xdeath gib-corpse declaration of a routed sprite.
        public static bool TryGetXdeathForTest(
            string sprite, out string lump, out float widthPx)
        {
            if (sprite != null && Sets.TryGetValue(sprite, out var s) &&
                s.XdeathLump != null)
            {
                lump = s.Sprite + s.XdeathLump;
                widthPx = s.XdeathWidthPx;
                return true;
            }
            lump = null;
            widthPx = 0f;
            return false;
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

        /// Test seam: per-frame native patch WIDTH for the frames that lie
        /// flat, 0 for the frames scaled by height. Parallel to the lump
        /// array from TryGetFrameTableForTest.
        public static bool TryGetFlatWidthsForTest(
            string sprite, out float[] flatWidthsPx)
        {
            if (sprite != null && Sets.TryGetValue(sprite, out var s))
            {
                flatWidthsPx = new float[s.FrameLumps.Length];
                for (int i = 0; i < flatWidthsPx.Length; i++)
                    flatWidthsPx[i] = s.FlatWidthPx(i);
                return true;
            }
            flatWidthsPx = null;
            return false;
        }

        /// Test seam: the fire frame that shows the shader muzzle flash and
        /// the flash LUT resource it loads. False when the sprite has none.
        public static bool TryGetMuzzleFlashForTest(
            string sprite, out int frameIndex, out string lutResource)
        {
            if (sprite != null && Sets.TryGetValue(sprite, out var s) &&
                s.MuzzleFlashFrame >= 0)
            {
                frameIndex = s.MuzzleFlashFrame;
                lutResource = ResourceRoot + s.Sprite + "/" + s.Sprite +
                              s.FrameLumps[s.MuzzleFlashFrame] + "_flash";
                return true;
            }
            frameIndex = -1;
            lutResource = null;
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
