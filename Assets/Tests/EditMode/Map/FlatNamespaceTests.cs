using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;
using Doom.Graphics;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;

namespace Doom.Map.Tests
{
    /// Vanilla keeps flats and wall textures in separate namespaces
    /// (R_FlatNumForName vs R_TextureNumForName); the name-keyed TextureCache
    /// used to hand the WALL composite to floors named STEP1/STEP2 (the two
    /// vanilla names existing in both namespaces). FlatKey aliases those to
    /// name+"_F" so the flat lump wins on floors/ceilings while the wall
    /// texture keeps its own key.
    public class FlatNamespaceTests
    {
        static string WadPath => Path.Combine(
            Application.dataPath, "StreamingAssets", "wads", "freedoom1.wad");

        [Test]
        public void FlatKey_aliases_only_names_that_collide_with_wall_textures()
        {
            if (!File.Exists(WadPath)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(WadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var cache = new TextureCache(wad, textures, palette, new DoomMaterialFactory());

            Assert.AreEqual("STEP1" + TextureCache.FlatSuffix, cache.FlatKey("STEP1"));
            Assert.AreEqual("STEP2" + TextureCache.FlatSuffix, cache.FlatKey("STEP2"));
            // Plain flat: no collision, key must stay stable (disk-pack keys!).
            Assert.AreEqual("FLOOR4_8", cache.FlatKey("FLOOR4_8"));
            // Wall-only name: unchanged (no flat lump to prefer).
            Assert.AreEqual("STARTAN2", cache.FlatKey("STARTAN2"));
        }

        [Test]
        public void Aliased_key_decodes_the_flat_lump_not_the_wall_composite()
        {
            if (!File.Exists(WadPath)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(WadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var cache = new TextureCache(wad, textures, palette, new DoomMaterialFactory());

            var flat = cache.GetTexture("STEP1" + TextureCache.FlatSuffix);
            Assert.AreEqual(64, flat.width, "aliased key must decode the 64x64 flat");
            Assert.AreEqual(64, flat.height);

            var wall = cache.GetTexture("STEP1");
            Assert.IsFalse(wall.width == 64 && wall.height == 64,
                "the bare name must keep decoding the wall composite");
        }

        [Test]
        public void Step_flats_are_the_only_namespace_collisions_in_freedoom()
        {
            // FlatKey special-cases collisions; this guards a future WAD swap
            // from silently introducing new ones.
            if (!File.Exists(WadPath)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(WadPath);
            var textures = TextureSet.Load(wad);

            var collisions = new System.Collections.Generic.List<string>();
            bool inFlats = false;
            for (int i = 0; i < wad.Directory.Count; i++)
            {
                var entry = wad.Directory[i];
                if (entry.Name == "F_START") { inFlats = true; continue; }
                if (entry.Name == "F_END") { inFlats = false; continue; }
                if (!inFlats || entry.Size != 64 * 64) continue;
                if (textures.Contains(entry.Name))
                    collisions.Add(entry.Name);
            }

            CollectionAssert.AreEquivalent(new[] { "STEP1", "STEP2" }, collisions);
        }
    }
}
