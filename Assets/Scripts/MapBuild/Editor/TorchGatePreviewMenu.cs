using System.IO;
using UnityEditor;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.MapBuild.Editor
{
    /// Было/стало panel for the Enhanced 3D firesticks: the native sprite next
    /// to the assembled torch (lathe stand + flame plume) rendered with its own
    /// shader from several angles, one per animation frame. A torch has to hold
    /// the sprite's read while the player walks around it, so the angles ARE
    /// the panel — a single front view proves nothing, since the front view is
    /// what both halves were measured from.
    ///
    /// Needs a graphics device: run the menu in the editor, or headless
    /// WITHOUT -nographics:
    ///   -batchmode -executeMethod
    ///   Doom.MapBuild.Editor.TorchGatePreviewMenu.DumpCli -quit
    /// Output: Logs/torch-gate/
    public static class TorchGatePreviewMenu
    {
        const int Cell = 256;
        const string Root = "ExperimentalTorches/";
        static readonly string[] Sprites =
            { "TBLU", "TGRN", "TRED", "SMBT", "SMGT", "SMRT" };
        // One yaw per flame frame: rotation and flicker are judged together.
        static readonly float[] Yaws = { 0f, 45f, 90f, 150f };

        [MenuItem("Tools/Doom/Dump Torch Gate Preview")]
        public static void Dump() => DumpCli();

        public static void DumpCli()
        {
            string repoRoot = Path.GetDirectoryName(Application.dataPath);
            string outDir = Path.Combine(repoRoot, "Logs", "torch-gate");
            Directory.CreateDirectory(outDir);

            var shader = Resources.Load<Shader>(Root + "DoomExperimentalTorch");
            if (shader == null)
            {
                Debug.LogError("TorchGatePreview: shader missing.");
                return;
            }

            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));

            // Fog is a world effect; the panel judges the torch itself.
            Shader.SetGlobalVector("_DoomFogParams", Vector4.zero);

            int cols = 1 + Yaws.Length;
            var sheet = new Texture2D(cols * Cell, Sprites.Length * Cell,
                                      TextureFormat.RGBA32, false);
            var background = new Color32(18, 18, 18, 255);
            var clear = new Color32[Cell * Cell];
            for (int i = 0; i < clear.Length; i++) clear[i] = background;
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < Sprites.Length; r++)
                    sheet.SetPixels32(c * Cell, r * Cell, Cell, Cell, clear);

            for (int s = 0; s < Sprites.Length; s++)
            {
                string sprite = Sprites[s];
                // SetPixels32 draws bottom-up, so the first sprite ends on top.
                int row = Sprites.Length - 1 - s;

                var native = Patch.Decode(wad.ReadLump(sprite + "A0"), palette);
                Blit(sheet, NativePanel(native, background), 0, row * Cell);

                for (int y = 0; y < Yaws.Length; y++)
                {
                    char frame = (char)('A' + (y % ExperimentalTorchModel.FrameCount));
                    var shot = RenderTorch(sprite, frame, shader,
                                           native.Height, Yaws[y], background);
                    if (shot == null) continue;
                    Blit(sheet, shot, (1 + y) * Cell, row * Cell);
                    Object.DestroyImmediate(shot);
                }
            }

            sheet.Apply();
            string path = Path.Combine(outDir, "torch-panel.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            File.WriteAllText(Path.Combine(outDir, "README.txt"),
                "Enhanced 3D firesticks — gate panel\n\n" +
                "Rows: " + string.Join(", ", Sprites) + " (top to bottom).\n" +
                "Columns: native sprite, then the 3D torch at yaw " +
                string.Join("/", Yaws) + " degrees, showing flame frames " +
                "A/B/C/D in turn.\n\n" +
                "What to judge: the stand must stay a turned metal stand from\n" +
                "every angle (highlight on the pole's middle, not smeared to\n" +
                "one side), and the flame must keep its hot core and taper\n" +
                "instead of reading as a coloured cone. No colour here is\n" +
                "invented — every texel comes from the lump itself.\n");
            Debug.Log($"TorchGatePreview: wrote {path}");
        }

        /// The native patch, nearest-scaled to fill the cell the way the 3D
        /// torch fills it.
        static Texture2D NativePanel(DecodedImage native, Color32 background)
        {
            var tex = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
            int scale = Mathf.Max(1, (int)(Cell * 0.86f) /
                                     Mathf.Max(native.Width, native.Height));
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

        /// Assembles the torch exactly as ExperimentalTorchModel does, in a
        /// space where the whole sprite is one unit tall.
        static Texture2D RenderTorch(string sprite, char frame, Shader shader,
                                     int patchHeight, float yawDegrees,
                                     Color32 background)
        {
            string dir = Root + sprite + "/";
            string flameName = sprite + frame + "0_flame";
            var standMesh = Resources.Load<GameObject>(dir + sprite + "_stand");
            var flameMesh = Resources.Load<GameObject>(dir + flameName);
            var standProfile = Resources.Load<Texture2D>(dir + sprite + "_stand_profile");
            var flameProfile = Resources.Load<Texture2D>(dir + flameName + "_profile");
            var standSpine = Resources.Load<Texture2D>(dir + sprite + "_stand_spine");
            var flameSpine = Resources.Load<Texture2D>(dir + flameName + "_spine");
            if (standMesh == null || flameMesh == null || standProfile == null ||
                flameProfile == null || standSpine == null || flameSpine == null)
            {
                Debug.LogError($"TorchGatePreview: {sprite}{frame} assets missing.");
                return null;
            }

            float flameHeight = flameProfile.height / (float)patchHeight;
            float standHeight = standProfile.height / (float)patchHeight;

            var holder = new GameObject("TorchPreview");
            holder.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            var standMaterial = NewMaterial(shader, standProfile, standSpine);
            var flameMaterial = NewMaterial(shader, flameProfile, flameSpine);
            Place(standMesh, holder.transform, 0f, standHeight, standMaterial);
            Place(flameMesh, holder.transform, standHeight, flameHeight, flameMaterial);

            var camGo = new GameObject("TorchPreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.58f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            camGo.transform.position = new Vector3(0f, 0.5f, -2f);
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
            Object.DestroyImmediate(standMaterial);
            Object.DestroyImmediate(flameMaterial);
            rt.Release();
            Object.DestroyImmediate(rt);
            return shot;
        }

        static Material NewMaterial(Shader shader, Texture2D profile, Texture2D spine)
        {
            var material = new Material(shader);
            material.mainTexture = profile;
            material.SetTexture("_SpineTex", spine);
            material.SetFloat("_SpineRange", 0.5f);
            material.SetFloat("_Exposure", 1f);
            return material;
        }

        static void Place(GameObject prefab, Transform parent, float bottom,
                          float height, Material material)
        {
            var pivot = new GameObject("Part");
            pivot.transform.SetParent(parent, worldPositionStays: false);
            pivot.transform.localPosition = new Vector3(0f, bottom, 0f);
            pivot.transform.localScale = Vector3.one * height;
            var instance = Object.Instantiate(prefab, pivot.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;
        }

        static void Blit(Texture2D sheet, Texture2D cell, int x, int y) =>
            sheet.SetPixels32(x, y, Cell, Cell, cell.GetPixels32());
    }
}
