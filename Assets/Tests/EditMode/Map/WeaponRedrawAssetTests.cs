using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Game;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.Map.Tests
{
    /// Weapon viewmodel redraw assets (Resources/EnhancedWeapons): every
    /// allowlisted lump ships a PNG at exactly 4x its native patch size whose
    /// opaque pixels stay INSIDE the native silhouette (the wave's healed
    /// halo — a redraw may run narrower than native, never wider, or the
    /// baked backdrop would ring the weapon on screen). Coverage is pinned
    /// to the WeaponTable so a fire sequence can never flicker between a
    /// redraw frame and a native one.
    public class WeaponRedrawAssetTests
    {
        static string FreedoomPath => Path.Combine(
            Application.streamingAssetsPath, "wads", "freedoom1.wad");

        static string RedrawDir => Path.Combine(
            Application.dataPath, "Resources", WeaponRedrawAllowlist.ResourcesFolder);

        static Texture2D LoadPng(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(path)),
                "PNG decode failed: " + path);
            return tex;
        }

        [Test]
        public void Every_allowlisted_weapon_redraw_is_4x_inside_native_silhouette()
        {
            if (!File.Exists(FreedoomPath)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(FreedoomPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));

            foreach (string name in WeaponRedrawAllowlist.Lumps)
            {
                string path = Path.Combine(RedrawDir, name + ".png");
                Assert.IsTrue(File.Exists(path), name + ": missing " + path);

                int lump = wad.FindLump(name);
                Assert.That(lump, Is.GreaterThanOrEqualTo(0), name + " not in WAD");
                var native = Patch.Decode(wad.ReadLump(lump), palette);

                var tex = LoadPng(path);
                try
                {
                    int scale = WeaponRedrawAllowlist.Scale;
                    Assert.AreEqual(native.Width * scale, tex.width,
                        name + ": redraw width must be exactly 4x");
                    Assert.AreEqual(native.Height * scale, tex.height,
                        name + ": redraw height must be exactly 4x");

                    // GetPixels32 rows run bottom-up; DecodedImage rows run
                    // top-down — flip once when mapping to the native texel.
                    var pixels = tex.GetPixels32();
                    int outside = 0;
                    for (int y = 0; y < tex.height; y++)
                    {
                        int nativeRow = (tex.height - 1 - y) / scale;
                        for (int x = 0; x < tex.width; x++)
                        {
                            if (pixels[y * tex.width + x].a < 128) continue;
                            int o = (nativeRow * native.Width + x / scale) * 4;
                            if (native.Rgba[o + 3] < 128) outside++;
                        }
                    }
                    Assert.AreEqual(0, outside,
                        name + ": opaque redraw pixels outside the native silhouette");
                }
                finally
                {
                    Object.DestroyImmediate(tex);
                }
            }
        }

        /// Orphan guard: every PNG in Resources/EnhancedWeapons must be
        /// allowlisted, or it ships in the build without ever being served.
        [Test]
        public void No_orphan_files_in_the_weapon_redraw_resources()
        {
            if (!Directory.Exists(RedrawDir))
            {
                Assert.Pass("no EnhancedWeapons resources yet");
                return;
            }

            foreach (string file in Directory.GetFiles(RedrawDir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                Assert.IsTrue(WeaponRedrawAllowlist.Contains(name),
                    name + ": PNG in Resources/EnhancedWeapons is not allowlisted");
            }
        }

        /// Flicker guard: every viewmodel lump the WeaponTable can request —
        /// idle, fire sequence and muzzle flash of every weapon — must be
        /// allowlisted. Partial coverage would flip a firing weapon between
        /// redraw and native texel densities mid-sequence.
        [Test]
        public void Weapon_table_frames_are_fully_covered()
        {
            var requested = new HashSet<string>();
            foreach (WeaponId id in System.Enum.GetValues(typeof(WeaponId)))
            {
                var def = WeaponTable.Get(id);
                requested.Add(LumpName(def.Sprite, def.IdleFrame));
                foreach (int frame in def.FireFrames)
                    requested.Add(LumpName(def.Sprite, frame));
                if (def.FlashSprite != null)
                    foreach (int frame in def.FlashFrames)
                        requested.Add(LumpName(def.FlashSprite, frame));
            }

            Assert.IsNotEmpty(requested);
            foreach (string lump in requested)
                Assert.IsTrue(WeaponRedrawAllowlist.Contains(lump),
                    lump + ": requested by the WeaponTable but not allowlisted");
        }

        static string LumpName(string sprite, int frame) =>
            sprite + (char)('A' + frame) + "0";
    }
}
