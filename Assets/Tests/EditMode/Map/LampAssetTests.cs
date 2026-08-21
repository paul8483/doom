using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Things;

namespace Doom.Map.Tests
{
    /// The floor lamp (2028) routes the regenerated COLUA0 TRELLIS.2 mesh
    /// (2026-08-21: the old head had dissolved into pixel mush) with a steady
    /// amber glow via the emission mask. Pin the asset chain OBJ -> MTL ->
    /// albedo (the white-POSS-corpse trap) and the mask the glow samples.
    public class LampAssetTests
    {
        static string AssetDir => Path.Combine(
            Application.dataPath, "Resources", "ExperimentalPickups", "COLUA0");

        [Test]
        public void Lamp_resolves_its_material_albedo_and_emission()
        {
            string mtlRef = FirstToken(Path.Combine(AssetDir, "COLUA0.obj"), "mtllib ");
            Assert.That(mtlRef, Is.EqualTo("COLUA0.mtl"),
                "a dangling mtllib imports the lamp with Unity's default " +
                "material, which renders pure white and trips nothing else");

            string mapRef = FirstToken(Path.Combine(AssetDir, mtlRef), "map_Kd ");
            Assert.That(mapRef, Is.EqualTo("COLUA0_albedo.png"));
            Assert.That(File.Exists(Path.Combine(AssetDir, mapRef)), Is.True);

            Assert.That(File.Exists(Path.Combine(AssetDir, "COLUA0_emission.png")),
                Is.True, "the steady amber glow samples this mask");
        }

        [Test]
        public void Lamp_mesh_and_mask_load_through_Resources()
        {
            Assert.That(
                Resources.Load<GameObject>("ExperimentalPickups/COLUA0/COLUA0"),
                Is.Not.Null, "COLUA0 does not load as a prefab");
            Assert.That(
                Resources.Load<Texture2D>("ExperimentalPickups/COLUA0/COLUA0_emission"),
                Is.Not.Null, "emission mask missing from Resources");
        }

        [Test]
        public void Lamp_glow_is_steady_not_blinking()
        {
            // The glow rides the discrete-blink path with no animation, which
            // parks it in the bright phase forever. If the lamp ever gains a
            // PickupAnimationTable entry, the glow starts blinking with it —
            // that would be a change of intent, so pin the absence.
            Assert.That(PickupAnimationTable.TryGet(2028, out _), Is.False,
                "2028 appeared in PickupAnimationTable — the lamp's steady " +
                "glow would start blinking; revisit ExperimentalPickupModel");
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
