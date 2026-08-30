using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Graphics;
using Doom.Things;
using Doom.Wad;

namespace Doom.Map.Tests
{
    /// The invulnerability sphere (2022 `PINV`) is a single frame-A TRELLIS.2
    /// mesh with a steady green-glass emission mask; the A-D sheen animation
    /// stays billboard-only (soulsphere precedent). The thing is absent from
    /// E1 maps — the asset rides ahead of later episodes. Pin the asset chain
    /// OBJ -> MTL -> albedo and the WAD facts the runtime leans on.
    ///
    /// The mesh is deliberately a flat medallion (thickness 0.25 of the
    /// diameter): TRELLIS read the engraved-star hint as a coin, and the user
    /// accepted it as-is (2026-08-30, «пусть будет не сфера») — do not
    /// re-roll it round without a new user decision.
    public class PinvAssetTests
    {
        const string Lump = "PINVA0";

        static string AssetDir => Path.Combine(
            Application.dataPath, "Resources", "ExperimentalPickups", Lump);

        [Test]
        public void Sphere_resolves_material_albedo_and_emission_mask()
        {
            string mtlRef = FirstToken(Path.Combine(AssetDir, Lump + ".obj"), "mtllib ");
            Assert.That(mtlRef, Is.EqualTo(Lump + ".mtl"),
                "a dangling mtllib imports the orb with Unity's default " +
                "material, which renders pure white and trips nothing else");

            string mapRef = FirstToken(Path.Combine(AssetDir, mtlRef), "map_Kd ");
            Assert.That(mapRef, Is.EqualTo(Lump + "_albedo.png"));
            Assert.That(File.Exists(Path.Combine(AssetDir, mapRef)), Is.True);

            // The runtime hands _EmissionMask this texture for the steady
            // glass glow; a Resources miss would silently kill the glow.
            Assert.That(
                Resources.Load<Texture2D>(
                    "ExperimentalPickups/" + Lump + "/" + Lump + "_emission"),
                Is.Not.Null, "green-glass emission mask does not load");
        }

        [Test]
        public void Sphere_loads_through_Resources()
        {
            Assert.That(
                Resources.Load<GameObject>(
                    "ExperimentalPickups/" + Lump + "/" + Lump),
                Is.Not.Null, Lump + " does not load as a prefab");
        }

        [Test]
        public void Visual_height_and_sheen_animation_match_the_WAD()
        {
            string path = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(path);

            // The runtime overrides SpriteHeightPx to 25 for 2022 (collision
            // height is 16); that number is only honest while the patch agrees.
            var header = Patch.ReadHeader(wad.ReadLump(Lump));
            Assert.That(ThingTable.TryGet(2022, out var def), Is.True);
            Assert.That(def.Sprite, Is.EqualTo("PINV"));
            Assert.That(header.Height, Is.EqualTo(25),
                "PINVA0 patch height changed — update the SpriteHeightPx " +
                "case for 2022 in ExperimentalPickupModel");

            // 2026-08-30 bit-verity fix: S_PINV spins A-D at 6 tics; the mesh
            // deliberately takes no blink animation (steady glow parked
            // bright), but the billboard sheen must survive on the sprite.
            Assert.That(PickupAnimationTable.TryGet(2022, out var anim), Is.True,
                "2022 lost its pickup animation — the billboard sheen is gone");
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
