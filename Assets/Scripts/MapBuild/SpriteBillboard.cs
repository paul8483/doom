using UnityEngine;
using Doom.Game;
using Doom.MapBuild.Rendering;

namespace Doom.MapBuild
{
    /// Camera-facing sprite. Cylindrical (rotates around world Y only). Picks one
    /// of 8 rotations from the angle between the camera and this object relative to
    /// the object's facing angle, mirroring when the sprite frame says so.
    [AddComponentMenu("Doom/Sprite Billboard")]
    public sealed class SpriteBillboard : MonoBehaviour
    {
        const float CrossFadeSeconds = 0.08f;
        const float PoseInterpRate = 35f;

        SpriteCache cache;
        string sprite;
        int frame;
        float worldScale;
        float doomAngleDeg;     // THINGS angle, DOOM convention (CCW from East)
        bool spawnCeiling;
        float ceilingY;         // world Y of the ceiling (for hanging things)

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        Transform cam;
        readonly Vector3[] quadVerts = new Vector3[4];
        bool lockRotation;
        int emissionLightHandle = -1;
        float emissionStrength;

        bool spectre;
        bool pickupUpscale;
        bool poseInterpolationEnabled;
        bool poseSeeded;
        Vector3 prevPos;
        Vector3 currPos;
        float prevAngleDeg;
        float currAngleDeg;
        float poseAlpha = 1f;

        float crossFadeLeft;
        Texture crossPrevTex;
        MaterialPropertyBlock mpb;

        public void Init(SpriteCache cache, string sprite, int frame, float worldScale,
                         float doomAngleDeg, bool spawnCeiling, float ceilingY)
        {
            this.cache = cache;
            this.sprite = sprite;
            this.frame = frame;
            this.worldScale = worldScale;
            this.doomAngleDeg = doomAngleDeg;
            this.spawnCeiling = spawnCeiling;
            this.ceilingY = ceilingY;

            meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = UnitQuad();

            currPos = prevPos = transform.position;
            currAngleDeg = prevAngleDeg = doomAngleDeg;
            poseSeeded = true;
            poseAlpha = 1f;
        }

        // The quad Mesh is created per instance in Init and never shared between
        // billboards. Destroy(gameObject) destroys components but not the Mesh
        // asset they reference, so without this every short-lived effect
        // (HitEffect PUFF/BLUD) would leak one native Mesh.
        void OnDestroy()
        {
            if (emissionLightHandle > 0)
            {
                EnhancedLightSystem.Instance?.Release(emissionLightHandle);
                emissionLightHandle = -1;
            }

            if (meshFilter != null && meshFilter.sharedMesh != null)
                Destroy(meshFilter.sharedMesh);
        }

        /// Sticky Enhanced decoration light handle; released on destroy.
        public void BindEmissionLight(int handle) => emissionLightHandle = handle;

        /// Per-instance Enhanced emission. Kept in a property block because sprite
        /// materials are cached and shared by every billboard using the same lump.
        public void SetEmission(float strength) =>
            emissionStrength = Mathf.Clamp(strength, 0f, 2f);

        public void SetSpectre(bool value) => spectre = value;
        public bool IsSpectre => spectre;
        public void SetPickupUpscale(bool value) => pickupUpscale = value;

        /// Called from MonsterController after gameplay pose updates (35 Hz).
        public void NotifyGameplayPose(Vector3 pos, float doomAngleDegrees)
        {
            // Pose interpolation is opt-in. Projectiles and transient effects move
            // their transforms every render frame and never call this method; using
            // their Init pose would visually pin them to the spawn point in Enhanced.
            poseInterpolationEnabled = true;

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

        /// Switch the billboard to a static frame with no rotation selection (corpse:
        /// DOOM death frames have no rotations — always rotation 0).
        public void SetStaticFrame(int newFrame)
        {
            frame = newFrame;
            lockRotation = true;
            crossFadeLeft = 0f;
            crossPrevTex = null;
        }

        /// Switch sprite prefix and frame (barrel BAR1 → BEXP explode sequence).
        public void SetSprite(string newSprite, int newFrame)
        {
            sprite = newSprite;
            frame = newFrame;
            lockRotation = true;
            crossFadeLeft = 0f;
            crossPrevTex = null;
        }

        /// Switch the animation frame while keeping rotation selection live
        /// (walking/attack/pain frames have 8 rotations; corpse uses SetStaticFrame).
        public void SetFrame(int newFrame)
        {
            if (newFrame != frame && meshRenderer != null)
            {
                var mat = meshRenderer.sharedMaterial;
                if (mat != null && mat.mainTexture != null && UseEnhancedSprites())
                {
                    crossPrevTex = mat.mainTexture;
                    crossFadeLeft = CrossFadeSeconds;
                }
            }

            frame = newFrame;
            lockRotation = false;
        }

        /// DOOM facing (+X = East) as a flat world direction for sight checks.
        public Vector3 FacingDirection
        {
            get
            {
                float rad = doomAngleDeg * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            }
        }

        public void SetDoomAngle(float degrees) => doomAngleDeg = degrees;

        public float DoomAngleDegrees => doomAngleDeg;
        public int CurrentFrame => frame;

        void LateUpdate()
        {
            if (cache == null) return;
            if (cam == null)
            {
                cam = ResolveCamera();
                if (cam == null) return;
            }

            var profile = ActiveProfile();
            bool interp = poseInterpolationEnabled &&
                          profile.Mode == GraphicsMode.Enhanced &&
                          profile.LitSprites;
            poseAlpha = Mathf.Clamp01(poseAlpha + Time.deltaTime * PoseInterpRate);

            float renderAngle = doomAngleDeg;
            Vector3 visualWorldOffset = Vector3.zero;
            if (interp && poseSeeded)
            {
                renderAngle = Mathf.LerpAngle(prevAngleDeg, currAngleDeg, poseAlpha);
                Vector3 visualPos = Vector3.Lerp(prevPos, currPos, poseAlpha);
                visualWorldOffset = visualPos - transform.position;
            }

            // 1) Face the camera around Y only (cylindrical billboard).
            Vector3 to = cam.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(-to, Vector3.up);

            // 2) Rotation index: angle from this object TO the camera, minus the
            //    object's facing, +22.5° for bucket centering → 0..7.
            //    When doomAngle == angToCam (facing the viewer) → rot 0 = DOOM '1' (front).
            int rotIndex = 0;
            if (!lockRotation)
            {
                float angToCam = Mathf.Atan2(to.z, to.x) * Mathf.Rad2Deg;
                float diff = Mod360(angToCam) - Mod360(renderAngle) + 22.5f;
                rotIndex = Mathf.FloorToInt(Mod360(diff) / 45f) & 7;
            }

            // 3) Resolve and apply the sprite material + quad size/anchor/mirror.
            bool useSpectre = spectre && profile.SpectreMaterial;
            var sm = ResolveSprite(frame, rotIndex, useSpectre);
            if (!sm.IsValid)
            {
                // Prefer front (0 = DOOM '1'), then back (4 = '5').
                sm = ResolveSprite(frame, 0, useSpectre);
                if (!sm.IsValid) sm = ResolveSprite(frame, 4, useSpectre);
                if (!sm.IsValid) { meshRenderer.enabled = false; return; }
            }
            meshRenderer.enabled = true;

            cache.Materials.RetargetSpriteMaterial(sm.Material, useSpectre);
            meshRenderer.sharedMaterial = sm.Material;

            ApplyPresentationProps(profile, sm.Material);
            ApplyQuadTransform(sm, visualWorldOffset);
        }

        SpriteMaterial ResolveSprite(int resolvedFrame, int rotationIndex, bool useSpectre) =>
            pickupUpscale
                ? cache.GetPickup(sprite, resolvedFrame, rotationIndex)
                : cache.Get(sprite, resolvedFrame, rotationIndex, useSpectre);

        void ApplyPresentationProps(GraphicsProfile profile, Material mat)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(mpb);

            float soft = profile.SoftFloorIntersection
                ? DoomMaterialFactory.SoftFloorFadeAmount : 0f;
            if (mat.HasProperty(DoomMaterialFactory.SoftFloorFadeProperty))
                mpb.SetFloat(DoomMaterialFactory.SoftFloorFadeProperty, soft);

            if (mat.HasProperty(DoomMaterialFactory.EmissionProperty))
                mpb.SetFloat(DoomMaterialFactory.EmissionProperty, emissionStrength);

            float cross = 0f;
            if (crossFadeLeft > 0f && crossPrevTex != null &&
                mat.HasProperty(DoomMaterialFactory.CrossFadeProperty))
            {
                crossFadeLeft -= Time.deltaTime;
                cross = Mathf.Clamp01(crossFadeLeft / CrossFadeSeconds);
                if (mat.HasProperty(DoomMaterialFactory.CrossTexProperty))
                    mpb.SetTexture(DoomMaterialFactory.CrossTexProperty, crossPrevTex);
            }
            else
            {
                crossFadeLeft = 0f;
                crossPrevTex = null;
            }

            if (mat.HasProperty(DoomMaterialFactory.CrossFadeProperty))
                mpb.SetFloat(DoomMaterialFactory.CrossFadeProperty, cross);

            meshRenderer.SetPropertyBlock(mpb);
        }

        // Rewrites the 4 quad verts in local space each frame (cheap). Local axes
        // ride the billboard rotation, so a local X offset stays "screen-horizontal"
        // and a local Y offset stays vertical. Size, mirror, and floor/ceiling anchor
        // are all baked into the vertex positions.
        void ApplyQuadTransform(SpriteMaterial sm, Vector3 visualWorldOffset)
        {
            float w = sm.Width * worldScale;
            float h = sm.Height * worldScale;
            float mirror = sm.Mirrored ? -1f : 1f;
            var mesh = meshFilter.sharedMesh;

            // Horizontal: align the sprite's origin (leftOffset px from the left) to
            // this object's XZ. Vertical: feet at the floor for normal things; for
            // hanging things the top edge sits at the ceiling.
            float xCenterOffset = (sm.Width * 0.5f - sm.LeftOffset) * worldScale * mirror;
            float bottomY, topY;
            if (spawnCeiling)
            {
                topY = ceilingY - transform.position.y;          // local
                bottomY = topY - h;
            }
            else
            {
                bottomY = (sm.TopOffset - sm.Height) * worldScale; // usually ≈ 0
                topY = bottomY + h;
            }

            Vector3 localOff = visualWorldOffset.sqrMagnitude > 1e-12f
                ? Quaternion.Inverse(transform.rotation) * visualWorldOffset
                : Vector3.zero;

            float halfW = w * 0.5f * mirror;
            quadVerts[0] = new Vector3(-halfW + xCenterOffset, bottomY, 0f) + localOff;
            quadVerts[1] = new Vector3( halfW + xCenterOffset, bottomY, 0f) + localOff;
            quadVerts[2] = new Vector3( halfW + xCenterOffset, topY,    0f) + localOff;
            quadVerts[3] = new Vector3(-halfW + xCenterOffset, topY,    0f) + localOff;
            mesh.vertices = quadVerts;
            mesh.RecalculateBounds();
        }

        static Transform ResolveCamera()
        {
            var ctx = GraphicsModeController.Instance != null
                ? GraphicsModeController.Instance.Context
                : null;
            if (ctx?.WorldCamera != null)
                return ctx.WorldCamera.transform;

            var main = Camera.main;
            if (main != null) return main.transform;

            var named = GameObject.Find("PlayerCamera");
            return named != null ? named.transform : null;
        }

        static GraphicsProfile ActiveProfile()
        {
            if (GraphicsModeController.Instance != null)
                return GraphicsModeController.Instance.ActiveProfile;
            return GraphicsProfile.Classic;
        }

        static bool UseEnhancedSprites()
        {
            var p = ActiveProfile();
            return p.Mode == GraphicsMode.Enhanced && p.LitSprites;
        }

        static float Mod360(float a)
        {
            a %= 360f;
            return a < 0f ? a + 360f : a;
        }

        static Mesh UnitQuad()
        {
            var m = new Mesh { name = "SpriteQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
                new Vector3(0.5f, 1f, 0f),  new Vector3(-0.5f, 1f, 0f),
            };
            m.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
