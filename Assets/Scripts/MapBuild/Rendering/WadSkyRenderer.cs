using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild.Rendering
{
    /// Camera-centered cylindrical WAD SKY1. Visible through F_SKY1 openings
    /// (those ceilings are empty meshes). Presentation only.
    public sealed class WadSkyRenderer : MonoBehaviour
    {
        public const string SkyTextureName = "SKY1";
        public const string ShaderName = "Doom/Sky";

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        Material skyMaterial;
        Texture2D skyTexture;
        Transform follow;
        bool active;
        float cylinderRadius = 64f;
        float cylinderHeight = 32f;

        public bool IsActive => active && meshRenderer != null && meshRenderer.enabled;
        public Material SkyMaterial => skyMaterial;

        public void Init(TextureCache textures, Transform cameraTransform, float worldScale)
        {
            follow = cameraTransform;
            cylinderRadius = Mathf.Max(16f, 2048f * worldScale);
            cylinderHeight = Mathf.Max(8f, 1024f * worldScale);

            meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = BuildCylinder(segments: 32, cylinderRadius, cylinderHeight);

            skyTexture = textures != null ? textures.GetTexture(SkyTextureName) : null;
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"WadSkyRenderer: shader '{ShaderName}' missing — falling back to ClassicOpaque");
                shader = Shader.Find(DoomMaterialFactory.ClassicOpaqueName);
            }
            if (shader == null)
            {
                Debug.LogWarning("WadSkyRenderer: no usable sky shader");
                meshRenderer.enabled = false;
                return;
            }

            skyMaterial = new Material(shader) { name = "DoomSky_Runtime" };
            if (skyTexture != null)
                skyMaterial.mainTexture = skyTexture;
            meshRenderer.sharedMaterial = skyMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
            active = false;
        }

        public void ApplyProfile(GraphicsProfile profile)
        {
            active = profile.Mode == GraphicsMode.Enhanced && profile.Sky;
            if (meshRenderer != null)
                meshRenderer.enabled = active && skyMaterial != null;
        }

        void LateUpdate()
        {
            if (!active || follow == null) return;

            // Translation follows camera; rotation locked so yaw/pitch come from view.
            transform.position = follow.position;
            transform.rotation = Quaternion.identity;

            if (skyMaterial == null) return;

            // Map yaw/pitch to UV offsets (seamless cylinder U).
            float yaw = follow.eulerAngles.y;
            float pitch = follow.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            skyMaterial.SetFloat("_YawOffset", yaw / 360f);
            skyMaterial.SetFloat("_PitchOffset", Mathf.Clamp(pitch / 90f, -1f, 1f) * 0.15f);
        }

        void OnDestroy()
        {
            if (skyMaterial != null)
            {
                Destroy(skyMaterial);
                skyMaterial = null;
            }
            if (meshFilter != null && meshFilter.sharedMesh != null)
                Destroy(meshFilter.sharedMesh);
        }

        static Mesh BuildCylinder(int segments, float radius, float height)
        {
            var mesh = new Mesh { name = "DoomSkyCylinder" };
            int vertCount = (segments + 1) * 2;
            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[segments * 6];

            float halfH = height * 0.5f;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float ang = t * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * radius;
                float z = Mathf.Sin(ang) * radius;
                verts[i] = new Vector3(x, -halfH, z);
                verts[i + segments + 1] = new Vector3(x, halfH, z);
                // Invert U so interior faces of the cylinder map left→right with yaw.
                uvs[i] = new Vector2(1f - t, 0f);
                uvs[i + segments + 1] = new Vector2(1f - t, 1f);
            }

            int ti = 0;
            for (int i = 0; i < segments; i++)
            {
                int b0 = i;
                int b1 = i + 1;
                int t0 = i + segments + 1;
                int t1 = i + 1 + segments + 1;
                // Inward-facing (camera inside).
                tris[ti++] = b0; tris[ti++] = t0; tris[ti++] = b1;
                tris[ti++] = b1; tris[ti++] = t0; tris[ti++] = t1;
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
