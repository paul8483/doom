using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.Map.Tests
{
    /// HUD redraw assets (Resources/EnhancedHud): every allowlisted status-bar
    /// patch ships a PNG at exactly 4x its native patch size whose transparent
    /// share stays close to the native's (digits and keys carry real holes —
    /// a redraw that fills or grows them would change the glyph's silhouette
    /// on screen). Decoding goes through raw PNG bytes so the suite stays
    /// green under -nographics.
    public class HudRedrawAssetTests
    {
        static string FreedoomPath => Path.Combine(
            Application.streamingAssetsPath, "wads", "freedoom1.wad");

        static string RedrawDir => Path.Combine(
            Application.dataPath, "Resources", HudRedrawAllowlist.ResourcesFolder);

        /// Holes share may drift this much (percentage points) from native:
        /// the wave-11 masked tolerance.
        const double MaxHolesDriftPp = 10.0;

        static Texture2D LoadPng(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(path)),
                "PNG decode failed: " + path);
            return tex;
        }

        [Test]
        public void Every_allowlisted_hud_redraw_is_4x_with_native_holes()
        {
            if (HudRedrawAllowlist.Lumps.Length == 0)
                Assert.Pass("allowlist empty — HUD redraws not installed yet");

            if (!File.Exists(FreedoomPath)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(FreedoomPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));

            foreach (string name in HudRedrawAllowlist.Lumps)
            {
                string path = Path.Combine(RedrawDir, name + ".png");
                Assert.IsTrue(File.Exists(path), name + ": missing " + path);

                int lump = wad.FindLump(name);
                Assert.That(lump, Is.GreaterThanOrEqualTo(0), name + " not in WAD");
                var native = Patch.Decode(wad.ReadLump(lump), palette);

                var tex = LoadPng(path);
                try
                {
                    Assert.AreEqual(native.Width * HudRedrawAllowlist.Scale, tex.width,
                        name + ": redraw width must be exactly 4x");
                    Assert.AreEqual(native.Height * HudRedrawAllowlist.Scale, tex.height,
                        name + ": redraw height must be exactly 4x");

                    var pixels = tex.GetPixels32();
                    int holes = 0;
                    for (int i = 0; i < pixels.Length; i++)
                        if (pixels[i].a < 128) holes++;
                    double redrawShare = 100.0 * holes / pixels.Length;

                    int nativeHoles = 0;
                    var rgba = native.Rgba;
                    for (int i = 3; i < rgba.Length; i += 4)
                        if (rgba[i] < 128) nativeHoles++;
                    double nativeShare = 400.0 * nativeHoles / rgba.Length;

                    Assert.That(redrawShare,
                        Is.EqualTo(nativeShare).Within(MaxHolesDriftPp),
                        name + ": transparent share drifted from native");
                }
                finally
                {
                    Object.DestroyImmediate(tex);
                }
            }
        }

        /// Orphan guard: every PNG in Resources/EnhancedHud must be
        /// allowlisted, or it ships in the build without ever being served.
        [Test]
        public void No_orphan_files_in_the_hud_redraw_resources()
        {
            if (!Directory.Exists(RedrawDir))
            {
                Assert.Pass("no EnhancedHud resources yet");
                return;
            }

            foreach (string file in Directory.GetFiles(RedrawDir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                Assert.IsTrue(HudRedrawAllowlist.Contains(name),
                    name + ": PNG in Resources/EnhancedHud is not allowlisted");
            }
        }
    }
}
