using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;
using Doom.Graphics;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;
using Doom.Specials;

namespace Doom.Map.Tests
{
    /// P_ChangeSwitchTexture swaps a sidedef to its SW1/SW2 counterpart —
    /// a name the map itself may never carry. MapLoader closes the WAD right
    /// after the build, so every counterpart must be decoded by the prewarm;
    /// otherwise the pressed switch paints the magenta placeholder (found on
    /// E1M3: SW2COMM is map-placed, SW1COMM appeared only after the press).
    public class SwitchCounterpartWarmTests
    {
        static string WadPath => Path.Combine(
            Application.dataPath, "StreamingAssets", "wads", "freedoom1.wad");

        [TestCase("E1M3", "SW2COMM", "SW1COMM")]
        [TestCase("E1M7", "SW1COMP", "SW2COMP")]
        public void Collected_names_include_the_switch_counterpart(
            string mapName, string placed, string counterpart)
        {
            if (!File.Exists(WadPath)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(WadPath);
            var map = MapData.Load(wad, mapName);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var cache = new TextureCache(wad, textures, palette, new DoomMaterialFactory());

            var mapNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var side in map.SideDefs)
            {
                mapNames.Add(side.UpperTexture);
                mapNames.Add(side.LowerTexture);
                mapNames.Add(side.MiddleTexture);
            }
            Assert.IsTrue(mapNames.Contains(placed), $"{mapName} should place {placed}");
            Assert.IsFalse(mapNames.Contains(counterpart),
                $"{counterpart} must be absent from {mapName}'s sidedefs for this pin to mean anything");

            var names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            MapLoader.CollectMapTextureNames(map, names, cache);
            Assert.IsTrue(names.Contains(counterpart),
                $"the warm list must bring {counterpart} along with {placed}");
        }

        [Test]
        public void Every_collected_name_on_every_E1_map_is_a_real_texture_or_flat()
        {
            // Counterparts are guarded by HasWallTexture; nothing in the warm
            // list may resolve to a placeholder.
            if (!File.Exists(WadPath)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(WadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            for (int m = 1; m <= 9; m++)
            {
                string mapName = "E1M" + m;
                var map = MapData.Load(wad, mapName);
                var cache = new TextureCache(wad, textures, palette, new DoomMaterialFactory());
                var names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                MapLoader.CollectMapTextureNames(map, names, cache);
                foreach (string name in names)
                {
                    cache.GetTexture(name);
                    Assert.IsFalse(cache.IsPlaceholderForTest(name),
                        $"{mapName}: warm name {name} decoded to a placeholder");
                }
            }
        }

        [Test]
        public void Build_after_wad_dispose_reports_a_placeholder()
        {
            // The in-game failure mode: TextureSet.Build on a disposed stream
            // does not throw — it returns a magenta checker of the texture's
            // own size, which the cache must flag rather than serve as texels.
            if (!File.Exists(WadPath)) Assert.Ignore("freedoom1.wad missing");
            TextureSet textures;
            Palette palette;
            {
                using var wad = WadFile.Open(WadPath);
                palette = new Palette(wad.ReadLump("PLAYPAL"));
                textures = TextureSet.Load(wad);
                textures.Build("SW1COMM", palette, out bool live);
                Assert.IsFalse(live, "with the WAD open SW1COMM must build for real");
            }
            var img = textures.Build("SW1COMM", palette, out bool placeholder);
            Assert.IsTrue(placeholder, "a closed WAD must be reported, not disguised as texels");
            Assert.AreEqual(64, img.Width);
            Assert.AreEqual(72, img.Height, "the checker takes the texture's own size");
        }

        [Test]
        public void Prewarm_decodes_the_counterpart_before_the_wad_closes()
        {
            // The actual failure mode: touch the counterpart only after Dispose.
            if (!File.Exists(WadPath)) Assert.Ignore("freedoom1.wad missing");
            TextureCache cache;
            {
                using var wad = WadFile.Open(WadPath);
                var map = MapData.Load(wad, "E1M3");
                var palette = new Palette(wad.ReadLump("PLAYPAL"));
                var textures = TextureSet.Load(wad);
                cache = new TextureCache(wad, textures, palette, new DoomMaterialFactory());
                MapLoader.PrewarmMapTextures(map, cache);
            }

            Assert.IsTrue(SwitchTextureRules.TryGetCounterpart("SW2COMM", out string other));
            var tex = cache.GetTexture(other);
            Assert.IsFalse(cache.IsPlaceholderForTest(other),
                "SW1COMM must come from the prewarm, not from a post-close decode");
            Assert.AreEqual(64, tex.width);
            Assert.AreEqual(72, tex.height, "SW1COMM is 64x72 in TEXTURE1; the placeholder is 64x64");
        }
    }
}
