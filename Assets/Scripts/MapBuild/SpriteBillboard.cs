using UnityEngine;

namespace Doom.MapBuild
{
    /// Camera-facing sprite. Cylindrical (rotates around world Y only). Picks one
    /// of 8 rotations from the angle between the camera and this object relative to
    /// the object's facing angle, mirroring when the sprite frame says so.
    [AddComponentMenu("Doom/Sprite Billboard")]
    public sealed class SpriteBillboard : MonoBehaviour
    {
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
        int currentLumpRotation = -1;
        bool currentMirror;

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

            meshFilter = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = UnitQuad();
        }

        void LateUpdate()
        {
            if (cache == null) return;
            if (cam == null)
            {
                var c = Camera.main;
                if (c == null) return;
                cam = c.transform;
            }

            // 1) Face the camera around Y only (cylindrical billboard).
            Vector3 to = cam.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(-to, Vector3.up);

            // 2) Rotation index: DOOM angle from this object TO the camera, minus the
            //    object's facing, offset by 202.5°, in 45° buckets → 0..7.
            float angToCam = Mathf.Atan2(to.z, to.x) * Mathf.Rad2Deg; // CCW from +X (East)
            float diff = angToCam - doomAngleDeg + 202.5f;
            int rotIndex = Mathf.FloorToInt(Mod360(diff) / 45f) & 7;

            // 3) Resolve and apply the sprite material + quad size/anchor/mirror.
            var sm = cache.Get(sprite, frame, rotIndex);
            if (!sm.IsValid)
            {
                // Fall back to the front rotation so something always shows.
                sm = cache.Get(sprite, frame, 0);
                if (!sm.IsValid) { meshRenderer.enabled = false; return; }
            }
            meshRenderer.enabled = true;

            if (rotIndex != currentLumpRotation || sm.Mirrored != currentMirror)
            {
                meshRenderer.sharedMaterial = sm.Material;
                currentLumpRotation = rotIndex;
                currentMirror = sm.Mirrored;
            }
            else
            {
                meshRenderer.sharedMaterial = sm.Material;
            }

            ApplyQuadTransform(sm);
        }

        // Rewrites the 4 quad verts in local space each frame (cheap). Local axes
        // ride the billboard rotation, so a local X offset stays "screen-horizontal"
        // and a local Y offset stays vertical. Size, mirror, and floor/ceiling anchor
        // are all baked into the vertex positions.
        void ApplyQuadTransform(SpriteMaterial sm)
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

            float halfW = w * 0.5f * mirror;
            var verts = new Vector3[4];
            verts[0] = new Vector3(-halfW + xCenterOffset, bottomY, 0f);
            verts[1] = new Vector3( halfW + xCenterOffset, bottomY, 0f);
            verts[2] = new Vector3( halfW + xCenterOffset, topY,    0f);
            verts[3] = new Vector3(-halfW + xCenterOffset, topY,    0f);
            mesh.vertices = verts;
            mesh.RecalculateBounds();
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
