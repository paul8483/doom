using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Things;

namespace Doom.Map.Tests
{
    /// The exploding barrel (2035) routes the re-rolled BAR1B0 TRELLIS.2 mesh
    /// (2026-08-21, user pick: ring lamps + green band read best) with a
    /// shader flash on its own S_BAR1 cadence. Pin the asset chain
    /// OBJ -> MTL -> albedo (the white-POSS-corpse trap), the emission mask
    /// the flash samples, and the WAD facts the runtime leans on.
    public class BarrelAssetTests
    {
        static string AssetDir => Path.Combine(
            Application.dataPath, "Resources", "ExperimentalPickups", "BAR1B0");

        [Test]
        public void Barrel_resolves_its_material_albedo_and_emission()
        {
            string mtlRef = FirstToken(Path.Combine(AssetDir, "BAR1B0.obj"), "mtllib ");
            Assert.That(mtlRef, Is.EqualTo("BAR1B0.mtl"),
                "a dangling mtllib imports the barrel with Unity's default " +
                "material, which renders pure white and trips nothing else");

            string mapRef = FirstToken(Path.Combine(AssetDir, mtlRef), "map_Kd ");
            Assert.That(mapRef, Is.EqualTo("BAR1B0_albedo.png"));
            Assert.That(File.Exists(Path.Combine(AssetDir, mapRef)), Is.True);

            Assert.That(File.Exists(Path.Combine(AssetDir, "BAR1B0_emission.png")),
                Is.True, "the lamp/band flash samples this mask");
        }

        [Test]
        public void Barrel_mesh_and_mask_load_through_Resources()
        {
            Assert.That(
                Resources.Load<GameObject>("ExperimentalPickups/BAR1B0/BAR1B0"),
                Is.Not.Null, "BAR1B0 does not load as a prefab");
            var mask = Resources.Load<Texture2D>(
                "ExperimentalPickups/BAR1B0/BAR1B0_emission");
            Assert.That(mask, Is.Not.Null, "emission mask missing from Resources");
        }

        [Test]
        public void Barrel_blink_cadence_matches_vanilla()
        {
            // The mesh flash and the Classic billboard share S_BAR1/S_BAR2
            // (A 6 tics -> B 6 tics, flash on frame B) — both read it from
            // BarrelRules, NOT PickupAnimationTable, which has no 2035 entry.
            // The first wiring looked the cadence up in the table, TryGet
            // returned false and the flash silently froze bright.
            Assert.That(BarrelRules.IdleFrames, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(BarrelRules.IdleTics, Is.EqualTo(new[] { 6, 6 }));
            Assert.That(PickupAnimationTable.TryGet(2035, out _), Is.False,
                "2035 appeared in PickupAnimationTable — revisit the blink " +
                "wiring in ExperimentalPickupModel so it is not applied twice");
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
