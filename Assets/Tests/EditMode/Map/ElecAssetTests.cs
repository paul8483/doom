using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Graphics;
using Doom.Things;
using Doom.Wad;

namespace Doom.Map.Tests
{
    /// The tall techno pillar (48 `ELEC`) is a TRELLIS.2 mesh routed through
    /// ExperimentalPickupModel like the green pillars. Pin the asset chain
    /// OBJ -> MTL -> albedo (the white-POSS-corpse failure mode), the steady
    /// blue-screen emission mask, and the WAD facts the runtime leans on.
    public class ElecAssetTests
    {
        const string Lump = "ELECA0";

        static string AssetDir => Path.Combine(
            Application.dataPath, "Resources", "ExperimentalPickups", Lump);

        [Test]
        public void Pillar_resolves_material_albedo_and_emission_mask()
        {
            string mtlRef = FirstToken(Path.Combine(AssetDir, Lump + ".obj"), "mtllib ");
            Assert.That(mtlRef, Is.EqualTo(Lump + ".mtl"),
                "a dangling mtllib imports the pillar with Unity's default " +
                "material, which renders pure white and trips nothing else");

            string mapRef = FirstToken(Path.Combine(AssetDir, mtlRef), "map_Kd ");
            Assert.That(mapRef, Is.EqualTo(Lump + "_albedo.png"));
            Assert.That(File.Exists(Path.Combine(AssetDir, mapRef)), Is.True);

            // The runtime hands _EmissionMask this texture for the steady
            // screen glow; a Resources miss would silently kill the glow.
            Assert.That(
                Resources.Load<Texture2D>(
                    "ExperimentalPickups/" + Lump + "/" + Lump + "_emission"),
                Is.Not.Null, "blue-screen emission mask does not load");
        }

        [Test]
        public void Pillar_loads_through_Resources()
        {
            Assert.That(
                Resources.Load<GameObject>(
                    "ExperimentalPickups/" + Lump + "/" + Lump),
                Is.Not.Null, Lump + " does not load as a prefab");
        }

        [Test]
        public void Patch_height_matches_the_collision_height()
        {
            string path = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(path);

            // The runtime has NO SpriteHeightPx override for 48: the default
            // (collision height) is only honest while the patch agrees.
            var header = Patch.ReadHeader(wad.ReadLump(Lump));
            Assert.That(ThingTable.TryGet(48, out var def), Is.True);
            Assert.That(def.Sprite, Is.EqualTo("ELEC"));
            Assert.That(header.Height, Is.EqualTo((int)def.Height),
                "ELECA0 patch height no longer equals the collision height — " +
                "add a SpriteHeightPx case in ExperimentalPickupModel");

            // Steady glow rides the parked-bright blink path; a decoration
            // animation appearing for 48 would start toggling the screens.
            Assert.That(DecorationAnimationTable.TryGet(48, out _), Is.False,
                "48 gained an animation: give ExperimentalPickupModel a real " +
                "blink cadence or the steady screens will flicker");
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
