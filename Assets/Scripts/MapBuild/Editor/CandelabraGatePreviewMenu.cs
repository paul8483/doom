using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.MapBuild.Editor
{
    /// Было/стало panel for the Enhanced 3D candelabra: the native sprite next
    /// to the assembled object — generated metal plus the three computed fires
    /// — from several angles. The fires have to sit INSIDE their lantern
    /// cages, and a reconstruction rebuilds the object with its own
    /// proportions, so a front view alone cannot show whether the fit holds.
    ///
    /// Needs a graphics device: run the menu in the editor, or headless
    /// WITHOUT -nographics:
    ///   -batchmode -executeMethod
    ///   Doom.MapBuild.Editor.CandelabraGatePreviewMenu.DumpCli -quit
    /// Output: Logs/candelabra-gate/
    public static class CandelabraGatePreviewMenu
    {
        const int Cell = 320;
        const string Sprite = "CBRA";
        const string Root = "ExperimentalTorches/" + Sprite + "/";
        static readonly float[] Yaws = { 0f, 45f, 90f, 150f };

        [MenuItem("Tools/Doom/Dump Candelabra Gate Preview")]
        public static void Dump() => DumpCli();

        public static void DumpCli()
        {
            string repoRoot = Path.GetDirectoryName(Application.dataPath);
            string outDir = Path.Combine(repoRoot, "Logs", "candelabra-gate");
            Directory.CreateDirectory(outDir);

            var fireShader = Resources.Load<Shader>(
                "ExperimentalTorches/DoomExperimentalTorch");
            var metalShader = Resources.Load<Shader>(
                "ExperimentalPickups/DoomExperimentalPickupUnlit");
            var metal = Resources.Load<GameObject>(Root + Sprite + "_stand_mesh");
            var table = Resources.Load<TextAsset>(Root + Sprite + "_fires");
            if (fireShader == null || metalShader == null || metal == null || table == null)
            {
                Debug.LogError("CandelabraGatePreview: assets missing.");
                return;
            }

            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var native = Patch.Decode(wad.ReadLump(Sprite + "A0"), palette);

            Shader.SetGlobalVector("_DoomFogParams", Vector4.zero);

            var background = new Color32(18, 18, 18, 255);
            var sheet = new Texture2D((1 + Yaws.Length) * Cell, Cell,
                                      TextureFormat.RGBA32, false);
            var clear = new Color32[Cell * Cell];
            for (int i = 0; i < clear.Length; i++) clear[i] = background;
            for (int c = 0; c < 1 + Yaws.Length; c++)
                sheet.SetPixels32(c * Cell, 0, Cell, Cell, clear);

            Blit(sheet, NativePanel(native, background), 0, 0);
            for (int y = 0; y < Yaws.Length; y++)
            {
                var shot = Render(metal, metalShader, fireShader, table.text,
                                  native.Height, Yaws[y], background);
                Blit(sheet, shot, (1 + y) * Cell, 0);
                Object.DestroyImmediate(shot);
            }

            sheet.Apply();
            string path = Path.Combine(outDir, "candelabra-panel.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            File.WriteAllText(Path.Combine(outDir, "README.txt"),
                "Enhanced 3D candelabra — gate panel\n\n" +
                "Columns: native sprite, then the assembled object at yaw " +
                string.Join("/", Yaws) + " degrees.\n\n" +
                "What to judge: each fire must sit INSIDE its cage with the\n" +
                "bars crossing in front of it, the metal must read as brass\n" +
                "and steel rather than plaster, and the whole thing must keep\n" +
                "the sprite's proportions — three lanterns on one column.\n");
            Debug.Log($"CandelabraGatePreview: wrote {path}");
        }

        static Texture2D Render(GameObject metal, Shader metalShader, Shader fireShader,
                                string table, int patchHeight, float yaw,
                                Color32 background)
        {
            var holder = new GameObject("CandelabraPreview");
            holder.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // The whole sprite is one unit tall here, exactly as the runtime
            // scales it by the patch height.
            float unit = 1f / patchHeight;
            var pivot = new GameObject("Metal");
            pivot.transform.SetParent(holder.transform, worldPositionStays: false);
            var instance = Object.Instantiate(metal, pivot.transform);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                var material = new Material(renderer.sharedMaterial);
                Texture albedo = material.mainTexture;
                material.shader = metalShader;
                material.mainTexture = albedo;
                material.SetFloat("_Exposure", 1f);
                material.SetFloat("_EmissionStrength", 0f);
                material.SetColor("_ColorTint", Color.white);
                renderer.sharedMaterial = material;
            }
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > 0.0001f)
                instance.transform.localScale *= 1f / bounds.size.y;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            instance.transform.position += new Vector3(
                -bounds.center.x, -bounds.min.y, -bounds.center.z);

            foreach (string raw in table.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string[] f = line.Split(' ');
                if (f.Length != 4) continue;
                var mesh = Resources.Load<GameObject>(Root + f[0]);
                var profile = Resources.Load<Texture2D>(Root + f[0] + "_profile");
                var spine = Resources.Load<Texture2D>(Root + f[0] + "_spine");
                if (mesh == null || profile == null || spine == null) continue;

                float x = float.Parse(f[1], CultureInfo.InvariantCulture) * unit;
                float y = float.Parse(f[2], CultureInfo.InvariantCulture) * unit;
                float h = float.Parse(f[3], CultureInfo.InvariantCulture) * unit;

                var firePivot = new GameObject(f[0]);
                firePivot.transform.SetParent(holder.transform, worldPositionStays: false);
                firePivot.transform.localPosition = new Vector3(x, y, 0f);
                firePivot.transform.localScale = Vector3.one * h;
                var fire = Object.Instantiate(mesh, firePivot.transform);
                var material = new Material(fireShader);
                material.mainTexture = profile;
                material.SetTexture("_SpineTex", spine);
                material.SetFloat("_SpineRange", 0.5f);
                material.SetFloat("_Exposure", 1f);
                foreach (var renderer in fire.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = material;
            }

            var camGo = new GameObject("CandelabraPreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.62f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            camGo.transform.position = new Vector3(0f, 0.5f, -2f);

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
                for (int x = 0; x < w; x++)
                {
                    int sx = x / scale, sy = native.Height - 1 - y / scale;
                    int o = (sy * native.Width + sx) * 4;
                    if (native.Rgba[o + 3] == 0) continue;
                    px[(oy + y) * Cell + ox + x] = new Color32(
                        native.Rgba[o], native.Rgba[o + 1], native.Rgba[o + 2], 255);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static void Blit(Texture2D sheet, Texture2D cell, int x, int y) =>
            sheet.SetPixels32(x, y, Cell, Cell, cell.GetPixels32());
    }
}
