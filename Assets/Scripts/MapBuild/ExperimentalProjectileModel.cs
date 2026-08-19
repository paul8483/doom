using System.Collections.Generic;
using UnityEngine;
using Doom.Game;
using Doom.MapBuild.Rendering;

namespace Doom.MapBuild
{
    /// Enhanced 3D presentation for a monster missile in flight (imp fireball).
    /// The mesh is a voxel ball generated from the sprite itself
    /// (Tools/make_fireball_mesh.py); the sprite's radial gradient is
    /// reproduced per fragment by Doom/ExperimentalFireball, because a solid
    /// ball's surface cannot show a core that lives inside it.
    ///
    /// Only the fly frames are covered. The explosion is a spray of loose
    /// pixels with no body to model, so impact hands presentation back to the
    /// billboard for good — the same pattern the barrel's BEXP and the
    /// monsters' xdeath gibs already use. Gameplay, collision and the save
    /// snapshot stay on the projectile root; Classic and Enhanced+3D Off keep
    /// the billboard.
    public sealed class ExperimentalProjectileModel : MonoBehaviour
    {
        const string ResourceRoot = "ExperimentalProjectiles/";
        /// Slow tumble so the baked per-voxel colour variants drift across the
        /// disc instead of standing still — the sprite's boiling, in 3D.
        const float SpinDegreesPerSecond = 180f;
        static readonly Vector3 SpinAxis = new Vector3(0.35f, 1f, 0.2f);

        /// Missile sprites with an accepted ball. Keyed by the same
        /// MonsterDef.MissileSprite the billboard uses.
        static readonly HashSet<string> Routed = new HashSet<string> { "BAL1" };

        SpriteBillboard billboard;
        MeshRenderer billboardRenderer;
        Transform pivot;
        Texture2D[] frameProfiles;
        Material material;

        int currentFrame;
        bool reverted;
        bool lastUseMesh;
        bool settingsControllerSeen;

        public bool HasModel => pivot != null;
        public bool ModelVisible => HasModel && pivot.gameObject.activeSelf;
        public int CurrentFrameForTest => currentFrame;
        public bool RevertedForTest => reverted;
        /// Test seam: the pivot the ball hangs from. Its localScale is the
        /// silhouette diameter and its localPosition the sprite's own centre,
        /// both taken from the WAD patch rather than measured.
        public Transform PivotForTest => pivot;
        public Texture CurrentProfileForTest =>
            material != null ? material.mainTexture : null;

        public static bool IsRoutedForTest(string sprite) =>
            sprite != null && Routed.Contains(sprite);

        /// Attach when the ball and EVERY fly frame's colour table are on
        /// disk — partial coverage would swap styles mid-flight. Size and
        /// anchor come from the WAD patch the billboard would have drawn, so
        /// the ball occupies exactly the sprite's place in the world.
        public static ExperimentalProjectileModel TryAttach(
            GameObject projectileRoot,
            SpriteCache cache,
            string sprite,
            int[] flyFrames,
            float worldScale,
            SpriteBillboard billboard)
        {
            if (projectileRoot == null || cache == null || sprite == null) return null;
            if (flyFrames == null || flyFrames.Length == 0) return null;
            if (!Routed.Contains(sprite)) return null;

            var mesh = Resources.Load<GameObject>(
                ResourceRoot + sprite + "/" + sprite);
            if (mesh == null) return null;

            var profiles = new Texture2D[flyFrames.Length];
            for (int i = 0; i < flyFrames.Length; i++)
            {
                string lump = sprite + (char)('A' + flyFrames[i]) + "0";
                profiles[i] = Resources.Load<Texture2D>(
                    ResourceRoot + sprite + "/" + lump + "_profile");
                if (profiles[i] == null) return null;
            }

            var patch = cache.Get(sprite, flyFrames[0], 0);
            if (!patch.IsValid) return null;

            var model = projectileRoot.AddComponent<ExperimentalProjectileModel>();
            model.Init(mesh, profiles, billboard,
                       diameter: patch.Width * worldScale,
                       // The billboard hangs the quad from the patch's top
                       // offset; the ball's centre is that quad's centre.
                       centerY: (patch.TopOffset - patch.Height * 0.5f) * worldScale);
            if (!model.HasModel)
            {
                Destroy(model);
                return null;
            }
            return model;
        }

        void Init(GameObject meshPrefab, Texture2D[] profiles,
                  SpriteBillboard sourceBillboard, float diameter, float centerY)
        {
            frameProfiles = profiles;
            billboard = sourceBillboard;
            billboardRenderer = GetComponent<MeshRenderer>();

            var pivotGo = new GameObject("Enhanced3DFireball");
            pivotGo.transform.SetParent(transform, worldPositionStays: false);
            pivotGo.transform.localPosition = new Vector3(0f, centerY, 0f);
            pivotGo.transform.localRotation = Quaternion.identity;
            // The OBJ is normalized to a unit bounding box, so the scale IS
            // the silhouette diameter — no bounds measuring needed.
            pivotGo.transform.localScale = Vector3.one * Mathf.Max(0.001f, diameter);

            var instance = Instantiate(meshPrefab, pivotGo.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            var renderers = instance.GetComponentsInChildren<Renderer>(
                includeInactive: true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning(
                    "ExperimentalProjectileModel: fireball mesh has no renderers.");
                Destroy(pivotGo);
                return;
            }

            var shader = Resources.Load<Shader>(
                ResourceRoot + "DoomExperimentalFireball");
            if (shader == null)
            {
                Debug.LogWarning(
                    "ExperimentalProjectileModel: fireball shader missing.");
                Destroy(pivotGo);
                return;
            }
            material = new Material(shader);
            material.SetFloat("_Exposure", 1f);
            material.mainTexture = frameProfiles[0];
            foreach (var renderer in renderers)
            {
                var slots = new Material[renderer.sharedMaterials.Length == 0
                    ? 1 : renderer.sharedMaterials.Length];
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
            }

            pivot = pivotGo.transform;
            currentFrame = 0;
            SettingsController.SettingsApplied += OnSettingsApplied;
            RefreshVisibility(force: true);
        }

        // -- Seams called from Projectile -----------------------------------

        /// Fly animation step (index into MonsterDef.MissileFlyFrames). Only
        /// the colour table changes — the two BAL1 fly frames are the same
        /// ball with different boiling.
        public void NotifyFlyFrame(int index)
        {
            if (reverted || frameProfiles == null || material == null) return;
            if (index < 0 || index >= frameProfiles.Length)
            {
                RevertToBillboard();
                return;
            }
            if (index == currentFrame) return;
            currentFrame = index;
            material.mainTexture = frameProfiles[index];
        }

        /// Impact: the explosion frames stay native, so hide the ball and let
        /// the billboard finish the sequence.
        public void RevertToBillboard()
        {
            reverted = true;
            if (pivot != null)
                pivot.gameObject.SetActive(false);
            if (billboardRenderer != null)
                billboardRenderer.enabled = true;
            if (billboard != null)
                billboard.enabled = true;
        }

        // -- Presentation cascade (same shape as the monster/pickup models) --

        void OnSettingsApplied(GameSettingsData _) => RefreshVisibility(force: true);

        void Update()
        {
            bool hasSettings = SettingsController.Instance != null;
            if (!hasSettings || !settingsControllerSeen)
            {
                settingsControllerSeen = hasSettings;
                RefreshVisibility(force: false);
            }
            if (ModelVisible)
                pivot.Rotate(SpinAxis.normalized,
                             SpinDegreesPerSecond * Time.deltaTime,
                             Space.Self);
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
            if (pivot != null)
                pivot.gameObject.SetActive(useMesh);
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
            if (material != null) Destroy(material);
        }
    }
}
