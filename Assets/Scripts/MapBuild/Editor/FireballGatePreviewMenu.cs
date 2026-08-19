using System.IO;
using UnityEditor;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.MapBuild.Editor
{
    /// Было/стало panel for the Enhanced 3D fireball: the native BAL1 fly
    /// frames next to the voxel ball rendered with its own shader from several
    /// angles. The ball's whole point is that it holds the sprite's read while
    /// turning, so the angles are the panel — a single view proves nothing.
    ///
    /// Needs a graphics device: run the menu in the editor, or headless
    /// WITHOUT -nographics:
    ///   -batchmode -executeMethod
    ///   Doom.MapBuild.Editor.FireballGatePreviewMenu.DumpCli -quit
    /// Output: Logs/fireball-gate/
    public static class FireballGatePreviewMenu
    {
        const int Cell = 256;
        static readonly string[] Frames = { "BAL1A0", "BAL1B0" };
        static readonly float[] Yaws = { 0f, 35f, 70f, 145f };

        [MenuItem("Tools/Doom/Dump Fireball Gate Preview")]
        public static void Dump() => DumpCli();

        public static void DumpCli()
        {
            string repoRoot = Path.GetDirectoryName(Application.dataPath);
            string outDir = Path.Combine(repoRoot, "Logs", "fireball-gate");
            Directory.CreateDirectory(outDir);

            var meshPrefab = Resources.Load<GameObject>(
                "ExperimentalProjectiles/BAL1/BAL1");
            var shader = Resources.Load<Shader>(
                "ExperimentalProjectiles/DoomExperimentalFireball");
            if (meshPrefab == null || shader == null)
            {
                Debug.LogError("FireballGatePreview: mesh or shader missing.");
                return;
            }

            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));

            // Fog is a world effect; the panel judges the ball itself.
            Shader.SetGlobalVector("_DoomFogParams", Vector4.zero);

            int cols = 1 + Yaws.Length;
            var sheet = new Texture2D(cols * Cell, Frames.Length * Cell,
                                      TextureFormat.RGBA32, false);
            var background = new Color32(18, 18, 18, 255);
            var clear = new Color32[Cell * Cell];
            for (int i = 0; i < clear.Length; i++) clear[i] = background;
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < Frames.Length; r++)
                    sheet.SetPixels32(c * Cell, r * Cell, Cell, Cell, clear);

            for (int f = 0; f < Frames.Length; f++)
            {
                // Rows are drawn bottom-up by SetPixels32, so frame A ends up
                // on top — the order the sprite animates in.
                int row = Frames.Length - 1 - f;

                var native = Patch.Decode(wad.ReadLump(Frames[f]), palette);
                Blit(sheet, NativePanel(native, background), 0, row * Cell);

                var profile = Resources.Load<Texture2D>(
                    "ExperimentalProjectiles/BAL1/" + Frames[f] + "_profile");
                if (profile == null)
                {
                    Debug.LogError($"FireballGatePreview: {Frames[f]} profile missing.");
                    continue;
                }
                var material = new Material(shader);
                material.SetFloat("_Exposure", 1f);
                material.mainTexture = profile;

                for (int y = 0; y < Yaws.Length; y++)
                {
                    var shot = RenderBall(meshPrefab, material, Yaws[y], background);
                    Blit(sheet, shot, (1 + y) * Cell, row * Cell);
                    Object.DestroyImmediate(shot);
                }
                Object.DestroyImmediate(material);
            }

            sheet.Apply();
            string path = Path.Combine(outDir, "fireball-panel.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            File.WriteAllText(Path.Combine(outDir, "README.txt"),
                "Enhanced 3D fireball — gate panel\n\n" +
                "Rows: BAL1A0 (top), BAL1B0 (bottom) — the two fly frames.\n" +
                "Columns: native sprite, then the voxel ball at yaw " +
                string.Join("/", Yaws) + " degrees.\n\n" +
                "What to judge: the ball must keep the sprite's read from every\n" +
                "angle — white core, yellow body, dark rim — while the\n" +
                "silhouette stays as chunky as the 15x15 sprite. Colour never\n" +
                "leaves BAL1's own texels; only the shape is new.\n");
            Debug.Log($"FireballGatePreview: wrote {path}");
        }

        /// The native patch, nearest-scaled to fill the cell like the sprite
        /// would fill the ball's footprint on screen.
        static Texture2D NativePanel(DecodedImage native, Color32 background)
        {
            var tex = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
            int scale = Mathf.Max(1, (int)(Cell * 0.72f) / Mathf.Max(native.Width,
                                                                    native.Height));
            int w = native.Width * scale, h = native.Height * scale;
            int ox = (Cell - w) / 2, oy = (Cell - h) / 2;
            var px = new Color32[Cell * Cell];
            for (int i = 0; i < px.Length; i++) px[i] = background;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // DecodedImage rows run top-down; Texture2D rows run up.
                    int sx = x / scale, sy = native.Height - 1 - y / scale;
                    int o = (sy * native.Width + sx) * 4;
                    if (native.Rgba[o + 3] == 0) continue;
                    px[(oy + y) * Cell + ox + x] = new Color32(
                        native.Rgba[o], native.Rgba[o + 1], native.Rgba[o + 2], 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static Texture2D RenderBall(GameObject prefab, Material material,
                                    float yawDegrees, Color32 background)
        {
            var holder = new GameObject("FireballPreview");
            var instance = Object.Instantiate(prefab, holder.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(18f, yawDegrees, 0f);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;

            var camGo = new GameObject("FireballPreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            // Half-height: the ball is 1.0 across, so 0.5 just fits it —
            // 0.7 leaves the same margin the native panel is drawn with.
            cam.orthographicSize = 0.7f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            camGo.transform.position = new Vector3(0f, 0f, -2f);
            camGo.transform.rotation = Quaternion.identity;

            var rt = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var shot = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
            shot.Apply();
            RenderTexture.active = prev;

            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(holder);
            rt.Release();
            Object.DestroyImmediate(rt);
            return shot;
        }

        static void Blit(Texture2D sheet, Texture2D cell, int x, int y) =>
            sheet.SetPixels32(x, y, Cell, Cell, cell.GetPixels32());
    }
}
