using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Doom.Map.Tests
{
    /// The health bonus (2014) routes the regenerated BON1A0 TRELLIS.2 flask
    /// (2026-08-21: real glass window framed by iron straps, mesh yawed so the
    /// window faces +Z) with a steady glass-only glow via the emission mask —
    /// the old global 0.65 emission boost is gone. Pin the asset chain
    /// OBJ -> MTL -> albedo (the white-POSS-corpse trap) and the mask.
    public class BonusAssetTests
    {
        static string AssetDir => Path.Combine(
            Application.dataPath, "Resources", "ExperimentalPickups", "BON1A0");

        [Test]
        public void Bonus_resolves_its_material_albedo_and_emission()
        {
            string mtlRef = FirstToken(Path.Combine(AssetDir, "BON1A0.obj"), "mtllib ");
            Assert.That(mtlRef, Is.EqualTo("BON1A0.mtl"),
                "a dangling mtllib imports the flask with Unity's default " +
                "material, which renders pure white and trips nothing else");

            string mapRef = FirstToken(Path.Combine(AssetDir, mtlRef), "map_Kd ");
            Assert.That(mapRef, Is.EqualTo("BON1A0_albedo.png"));
            Assert.That(File.Exists(Path.Combine(AssetDir, mapRef)), Is.True);

            Assert.That(File.Exists(Path.Combine(AssetDir, "BON1A0_emission.png")),
                Is.True, "the steady glass glow samples this mask");
        }

        [Test]
        public void Bonus_mesh_and_mask_load_through_Resources()
        {
            Assert.That(
                Resources.Load<GameObject>("ExperimentalPickups/BON1A0/BON1A0"),
                Is.Not.Null, "BON1A0 does not load as a prefab");
            Assert.That(
                Resources.Load<Texture2D>("ExperimentalPickups/BON1A0/BON1A0_emission"),
                Is.Not.Null, "emission mask missing from Resources");
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
