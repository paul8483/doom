using System;
using System.IO;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.MapBuild.Editor
{
    /// Gate 0 diagnostic: dumps native | Super-xBR+Sharpen | Real-ESRGAN triptychs
    /// for representative sprites/HUD into Logs/neural-preview/&lt;model&gt;/.
    /// Pipeline mirrors the planned runtime: AlphaBleed → neural RGB + Super-xBR
    /// alpha → merge (no Sharpen on neural). Does not touch runtime caches.
    public static class NeuralSpritePreviewMenu
    {
        const int DisplayScale = 8;
        const int Gap = 12;

        const string Anime6BPath =
            "Assets/ThirdParty/RealEsrgan/RealESRGAN_x4plus_anime_6B.onnx";
        const string AnimeVideoPath =
            "Assets/ThirdParty/RealEsrgan/realesr-animevideov3_x4.onnx";

        // Spec Gate 0 set: imp / zombie / shotgun guy ×8 rotations; shotgun
        // viewmodel; medikit; STBAR; consecutive imp walk frames (front).
        static readonly string[] LumpNames =
        {
            "TROOA1", "TROOA2", "TROOA3", "TROOA4",
            "TROOA5", "TROOA6", "TROOA7", "TROOA8",
            "POSSA1", "POSSA2", "POSSA3", "POSSA4",
            "POSSA5", "POSSA6", "POSSA7", "POSSA8",
            "SPOSA1", "SPOSA2", "SPOSA3", "SPOSA4",
            "SPOSA5", "SPOSA6", "SPOSA7", "SPOSA8",
            "SHTGA0",
            "MEDIA0",
            "STBAR",
            "TROOB1", "TROOC1", "TROOD1", // walk after TROOA1 above
        };

        struct ModelCandidate
        {
            public string FolderName;
            public string AssetPath;
        }

        [MenuItem("Tools/Doom/Dump Neural Sprite Preview")]
        public static void Dump()
        {
            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            string outRoot = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "Logs", "neural-preview");
            Directory.CreateDirectory(outRoot);

            var candidates = new[]
            {
                new ModelCandidate { FolderName = "anime_6B", AssetPath = Anime6BPath },
                new ModelCandidate { FolderName = "animevideov3", AssetPath = AnimeVideoPath },
            };

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));

            foreach (var candidate in candidates)
            {
                string outDir = Path.Combine(outRoot, candidate.FolderName);
                Directory.CreateDirectory(outDir);

                var modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(candidate.AssetPath);
                if (modelAsset == null)
                {
                    Debug.LogError(
                        $"NeuralSpritePreview: ModelAsset missing at {candidate.AssetPath}");
                    continue;
                }

                Worker worker = null;
                try
                {
                    var model = ModelLoader.Load(modelAsset);
                    LogModelIO(candidate.FolderName, model);
                    worker = CreateWorker(model);
                    if (worker == null)
                    {
                        Debug.LogError(
                            $"NeuralSpritePreview: no Sentis backend for {candidate.FolderName}");
                        continue;
                    }

                    int written = 0;
                    foreach (string name in LumpNames)
                    {
                        int idx = wad.FindLump(name);
                        if (idx < 0)
                        {
                            Debug.LogWarning($"NeuralSpritePreview: lump {name} missing, skipped");
                            continue;
                        }

                        var native = Patch.Decode(wad.ReadLump(idx), palette);
                        if (!TryWriteTriptych(worker, native, name, outDir, out string err))
                        {
                            Debug.LogError(
                                $"NeuralSpritePreview: {candidate.FolderName}/{name}: {err}");
                            continue;
                        }
                        written++;
                    }

                    Debug.Log(
                        $"NeuralSpritePreview: {candidate.FolderName} wrote {written} PNGs to {outDir}");
                }
                finally
                {
                    worker?.Dispose();
                }
            }

            Debug.Log($"NeuralSpritePreview: done → {outRoot}");
        }

        /// Batchmode entry (CPU backend under -nographics).
        public static void DumpBatch()
        {
            Dump();
            EditorApplication.Exit(0);
        }

        static Worker CreateWorker(Model model)
        {
            // Prefer GPU; fall back to Burst CPU (needed for -nographics dumps).
            foreach (var backend in new[] { BackendType.GPUCompute, BackendType.CPU })
            {
                try
                {
                    var worker = new Worker(model, backend);
                    Debug.Log($"NeuralSpritePreview: using backend {backend}");
                    return worker;
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"NeuralSpritePreview: backend {backend} unavailable: {e.Message}");
                }
            }
            return null;
        }

        static void LogModelIO(string label, Model model)
        {
            foreach (var input in model.inputs)
                Debug.Log($"NeuralSpritePreview: {label} input '{input.name}' {input.dataType} {input.shape}");
            foreach (var output in model.outputs)
                Debug.Log($"NeuralSpritePreview: {label} output '{output.name}'");
        }

        static bool TryWriteTriptych(
            Worker worker,
            DecodedImage native,
            string name,
            string outDir,
            out string error)
        {
            error = null;
            try
            {
                var bled = AlphaBleedGuard.Dilate(native);
                var superXbr = TextureCache.BuildEnhanced4XDecoded(
                    native, PixelWrapMode.Clamp, applyDedither: true, applyAlphaBleed: true);
                var superSharp = SharpenFilter.Apply(superXbr);

                if (!TryNeuralUpscale(worker, bled, superXbr, out var neural, out error))
                    return false;

                WriteTriptychPng(native, superSharp, neural, Path.Combine(outDir, $"{name}.png"));
                return true;
            }
            catch (Exception e)
            {
                error = e.ToString();
                return false;
            }
        }

        static bool TryNeuralUpscale(
            Worker worker,
            DecodedImage bledNative,
            DecodedImage superXbrRgba4x,
            out DecodedImage merged,
            out string error)
        {
            merged = null;
            error = null;

            int w = bledNative.Width;
            int h = bledNative.Height;

            using var input = new Tensor<float>(new TensorShape(1, 3, h, w));
            FillRgbTensor(bledNative, input);
            worker.Schedule(input);

            var output = worker.PeekOutput() as Tensor<float>;
            if (output == null)
            {
                error = "PeekOutput was not Tensor<float>";
                return false;
            }

            using var cpu = output.ReadbackAndClone();
            if (cpu.shape.rank != 4 || cpu.shape[1] < 3)
            {
                error = $"Unexpected output shape {cpu.shape}";
                return false;
            }

            int outH = cpu.shape[2];
            int outW = cpu.shape[3];
            if (outW != w * 4 || outH != h * 4)
            {
                error = $"Expected {w * 4}x{h * 4} output, got {outW}x{outH}";
                return false;
            }

            if (superXbrRgba4x.Width != outW || superXbrRgba4x.Height != outH)
            {
                error = "Super-xBR size mismatch vs neural output";
                return false;
            }

            var rgba = new byte[outW * outH * 4];
            var alpha = superXbrRgba4x.Rgba;
            for (int y = 0; y < outH; y++)
            {
                for (int x = 0; x < outW; x++)
                {
                    int i = (y * outW + x) * 4;
                    rgba[i] = Quantize(cpu[0, 0, y, x]);
                    rgba[i + 1] = Quantize(cpu[0, 1, y, x]);
                    rgba[i + 2] = Quantize(cpu[0, 2, y, x]);
                    rgba[i + 3] = alpha[i + 3];
                }
            }

            merged = new DecodedImage(outW, outH, rgba);
            return true;
        }

        static void FillRgbTensor(DecodedImage src, Tensor<float> dst)
        {
            int w = src.Width;
            int h = src.Height;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = src.GetPixel(x, y);
                    dst[0, 0, y, x] = p.r / 255f;
                    dst[0, 1, y, x] = p.g / 255f;
                    dst[0, 2, y, x] = p.b / 255f;
                }
            }
        }

        static byte Quantize(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 1f) return 255;
            return (byte)(v * 255f + 0.5f);
        }

        static void WriteTriptychPng(
            DecodedImage native,
            DecodedImage superSharp,
            DecodedImage neural,
            string path)
        {
            var panels = new[] { native, superSharp, neural };
            int w = native.Width;
            int h = native.Height;
            int panelW = w * DisplayScale;
            int outH = h * DisplayScale;
            int outW = panelW * panels.Length + Gap * (panels.Length - 1);
            var colors = new Color32[outW * outH];
            var gapColor = new Color32(24, 24, 24, 255);
            var bg = new Color32(60, 30, 60, 255);

            for (int y = 0; y < outH; y++)
            {
                int texRow = (outH - 1 - y) * outW;
                for (int x = 0; x < outW; x++)
                {
                    int panel = x / (panelW + Gap);
                    int px = x - panel * (panelW + Gap);
                    Color32 c;
                    if (px >= panelW)
                    {
                        c = gapColor;
                    }
                    else
                    {
                        var img = panels[panel];
                        int div = img.Width == w ? DisplayScale : DisplayScale / 4;
                        var p = img.GetPixel(px / div, y / div);
                        c = p.a <= 128 ? bg : new Color32(p.r, p.g, p.b, 255);
                    }
                    colors[texRow + x] = c;
                }
            }

            var tex = new Texture2D(outW, outH, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(colors);
                tex.Apply(false);
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
