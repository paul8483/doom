using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild.Rendering
{
    /// Camera-centered WAD SKY1 sphere. Visible through F_SKY1 openings
    /// (those ceilings are empty meshes). Presentation only.
    ///
    /// Uses a closed sphere (not an open cylinder): looking straight up must
    /// still sample SKY1, otherwise the camera clear color shows through.
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

        public bool IsActive => active && meshRenderer != null && meshRenderer.enabled;
        public Material SkyMaterial => skyMaterial;

        public void Init(TextureCache textures, Transform cameraTransform, float worldScale)
        {
            follow = cameraTransform;
            float radius = Mathf.Max(16f, 2048f * worldScale);

            meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = BuildSphere(lonSegments: 48, latSegments: 24, radius);

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
            {
                skyTexture.wrapMode = TextureWrapMode.Repeat;
                skyTexture.filterMode = FilterMode.Point;
                skyMaterial.mainTexture = skyTexture;
                if (skyMaterial.HasProperty("_MainTex"))
                    skyMaterial.SetTexture("_MainTex", skyTexture);
            }
            skyMaterial.SetFloat("_YawOffset", 0f);
            skyMaterial.SetFloat("_PitchOffset", 0f);
            skyMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
            meshRenderer.sharedMaterial = skyMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            // Outward sphere + Cull Front in shader = see interior.
            meshRenderer.enabled = false;
            active = false;
        }

        public void ApplyProfile(GraphicsProfile profile)
        {
            // Sky is WAD content (SKY1), not an Enhanced-only effect — show whenever
            // the profile requests it so F_SKY1 openings are never a solid clear color.
            active = profile.Sky;
            if (meshRenderer != null)
                meshRenderer.enabled = active && skyMaterial != null;
        }

        void LateUpdate()
        {
            if (!active || follow == null) return;

            // Translation follows camera; rotation stays world-locked so yaw/pitch
            // come from the view direction through the sphere.
            transform.position = follow.position;
            transform.rotation = Quaternion.identity;
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

        /// Inward-facing UV sphere. U = longitude (panorama), V = latitude
        /// (0 at -Y / bottom, 1 at +Y / top — matches SKY1 mountain→sky layout).
        static Mesh BuildSphere(int lonSegments, int latSegments, float radius)
        {
            var mesh = new Mesh { name = "DoomSkySphere" };
            int vertsU = lonSegments + 1;
            int vertsV = latSegments + 1;
            var verts = new Vector3[vertsU * vertsV];
            var uvs = new Vector2[verts.Length];
            var tris = new int[lonSegments * latSegments * 6];

            for (int v = 0; v < vertsV; v++)
            {
                float v01 = (float)v / latSegments;
                // phi: 0 at +Y (top), PI at -Y (bottom)
                float phi = v01 * Mathf.PI;
                float y = Mathf.Cos(phi);
                float ringR = Mathf.Sin(phi);
                for (int u = 0; u < vertsU; u++)
                {
                    float u01 = (float)u / lonSegments;
                    float theta = u01 * Mathf.PI * 2f;
                    float x = ringR * Mathf.Cos(theta);
                    float z = ringR * Mathf.Sin(theta);
                    int idx = v * vertsU + u;
                    verts[idx] = new Vector3(x, y, z) * radius;
                    // Invert U for interior view; invert V so texture top (sky) is at +Y.
                    uvs[idx] = new Vector2(1f - u01, 1f - v01);
                }
            }

            int ti = 0;
            for (int v = 0; v < latSegments; v++)
            {
                for (int u = 0; u < lonSegments; u++)
                {
                    int i0 = v * vertsU + u;
                    int i1 = i0 + 1;
                    int i2 = i0 + vertsU;
                    int i3 = i2 + 1;
                    // Outward-facing: Doom/Sky uses Cull Front so the camera
                    // inside sees the backfaces (standard skybox sphere).
                    tris[ti++] = i0; tris[ti++] = i1; tris[ti++] = i2;
                    tris[ti++] = i1; tris[ti++] = i3; tris[ti++] = i2;
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
