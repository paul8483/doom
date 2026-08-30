using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Graphics;
using Doom.Things;
using Doom.Wad;

namespace Doom.Map.Tests
{
    /// The soulsphere (2013 `SOUL`) is a single frame-A TRELLIS.2 mesh with a
    /// steady golden-shell emission mask; the laugh animation A-D stays
    /// billboard-only (BON1 star-glint precedent). Pin the asset chain
    /// OBJ -> MTL -> albedo (the white-POSS-corpse failure mode) and the WAD
    /// facts the runtime leans on.
    public class SoulAssetTests
    {
        const string Lump = "SOULA0";

        static string AssetDir => Path.Combine(
            Application.dataPath, "Resources", "ExperimentalPickups", Lump);

        [Test]
        public void Soulsphere_resolves_material_albedo_and_emission_mask()
        {
            string mtlRef = FirstToken(Path.Combine(AssetDir, Lump + ".obj"), "mtllib ");
            Assert.That(mtlRef, Is.EqualTo(Lump + ".mtl"),
                "a dangling mtllib imports the orb with Unity's default " +
                "material, which renders pure white and trips nothing else");

            string mapRef = FirstToken(Path.Combine(AssetDir, mtlRef), "map_Kd ");
            Assert.That(mapRef, Is.EqualTo(Lump + "_albedo.png"));
            Assert.That(File.Exists(Path.Combine(AssetDir, mapRef)), Is.True);

            // The runtime hands _EmissionMask this texture for the steady
            // golden glow; a Resources miss would silently kill the glow.
            Assert.That(
                Resources.Load<Texture2D>(
                    "ExperimentalPickups/" + Lump + "/" + Lump + "_emission"),
                Is.Not.Null, "golden-shell emission mask does not load");
        }

        [Test]
        public void Soulsphere_loads_through_Resources()
        {
            Assert.That(
                Resources.Load<GameObject>(
                    "ExperimentalPickups/" + Lump + "/" + Lump),
                Is.Not.Null, Lump + " does not load as a prefab");
        }

        [Test]
        public void Visual_height_and_laugh_animation_match_the_WAD()
        {
            string path = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(path);

            // The runtime overrides SpriteHeightPx to 25 for 2013 (collision
            // height is 16); that number is only honest while the patch agrees.
            var header = Patch.ReadHeader(wad.ReadLump(Lump));
            Assert.That(ThingTable.TryGet(2013, out var def), Is.True);
            Assert.That(def.Sprite, Is.EqualTo("SOUL"));
            Assert.That(header.Height, Is.EqualTo(25),
                "SOULA0 patch height changed — update the SpriteHeightPx " +
                "case for 2013 in ExperimentalPickupModel");

            // The billboard's laugh cadence lives in PickupAnimationTable and
            // must survive: the mesh deliberately takes no blink animation
            // (steady glow parked bright), but Classic and Enhanced+3D Off
            // still play the four-frame laugh on the sprite.
            Assert.That(PickupAnimationTable.TryGet(2013, out var anim), Is.True,
                "2013 lost its pickup animation — the billboard laugh is gone");
            Assert.That(anim.Frames.Length, Is.EqualTo(4));
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
