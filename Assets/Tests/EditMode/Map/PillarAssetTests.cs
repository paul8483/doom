using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Graphics;
using Doom.Things;
using Doom.Wad;

namespace Doom.Map.Tests
{
    /// The green pillars (30 `COL1`, 31 `COL2`) are TRELLIS.2 meshes routed
    /// through ExperimentalPickupModel like the lamp and the trees. Pin the
    /// asset chain OBJ -> MTL -> albedo (the failure that shipped white POSS
    /// corpses in 2026-08-16 with every other test still green) and the WAD
    /// facts the runtime constants lean on.
    public class PillarAssetTests
    {
        static readonly (int DoomEdNum, string Lump, int PatchHeight)[] Pillars =
        {
            (30, "COL1A0", 56),
            (31, "COL2A0", 41),
            (32, "COL3A0", 55),
        };

        static string AssetDir(string lump) => Path.Combine(
            Application.dataPath, "Resources", "ExperimentalPickups", lump);

        [Test]
        public void Each_pillar_resolves_its_material_and_albedo()
        {
            foreach (var (_, lump, _) in Pillars)
            {
                string dir = AssetDir(lump);
                string mtlRef = FirstToken(Path.Combine(dir, lump + ".obj"), "mtllib ");
                Assert.That(mtlRef, Is.EqualTo(lump + ".mtl"),
                    "a dangling mtllib imports the pillar with Unity's default " +
                    "material, which renders pure white and trips nothing else");

                string mapRef = FirstToken(Path.Combine(dir, mtlRef), "map_Kd ");
                Assert.That(mapRef, Is.EqualTo(lump + "_albedo.png"));
                Assert.That(File.Exists(Path.Combine(dir, mapRef)), Is.True);
            }
        }

        [Test]
        public void Each_pillar_loads_through_Resources()
        {
            // On disk is not the same as loadable: the runtime asks Resources
            // for the prefab, and a miss silently falls back to the billboard.
            foreach (var (_, lump, _) in Pillars)
                Assert.That(
                    Resources.Load<GameObject>("ExperimentalPickups/" + lump + "/" + lump),
                    Is.Not.Null, lump + " does not load as a prefab");
        }

        [Test]
        public void Height_constants_match_the_wad_patches()
        {
            string path = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(path);

            foreach (var (doomEdNum, lump, patchHeight) in Pillars)
            {
                // The runtime scales the mesh to the sprite's visual height,
                // pinned as a constant in ExperimentalPickupModel — keep that
                // constant honest against the real patch.
                var header = Patch.ReadHeader(wad.ReadLump(lump));
                Assert.That(header.Height, Is.EqualTo(patchHeight),
                    $"{lump}: SpriteHeightPx constant no longer matches the WAD");

                Assert.That(ThingTable.TryGet(doomEdNum, out var def), Is.True);
                Assert.That(def.Sprite, Is.EqualTo(lump.Substring(0, 4)),
                    $"thing {doomEdNum} draws {def.Sprite}, not {lump}");
            }
        }

        static string FirstToken(string file, string prefix)
        {
            Assert.That(File.Exists(file), Is.True, $"{file} missing");
            foreach (string line in File.ReadLines(file))
                if (line.StartsWith(prefix))
                    return line.Substring(prefix.Length).Trim();
            return null;
        }
    }
}
