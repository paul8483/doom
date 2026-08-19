using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Graphics;
using Doom.MapBuild;
using Doom.Things;
using Doom.Wad;

namespace Doom.Map.Tests
{
    /// The Enhanced torches are computed by Tools/make_torch_model.py and then
    /// trusted blindly by the runtime: each part is scaled straight to a row
    /// count with no bounds measuring, the flame/stand split is read off the
    /// colour table's height, and the shader treats object space as a plume of
    /// height 1 standing on y=0. Pin that contract here, plus the asset chain
    /// OBJ -> MTL -> texture (the failure that shipped white POSS corpses in
    /// 2026-08-16 with every other test still green).
    public class TorchAssetTests
    {
        static string AssetDir(string sprite) => Path.Combine(
            Application.dataPath, "Resources", "ExperimentalTorches", sprite);

        static string WadPath() => Path.Combine(
            Application.streamingAssetsPath, "wads", "freedoom1.wad");

        static IEnumerable<string> Sprites()
        {
            foreach (var pair in ExperimentalTorchModel.RoutedForTest)
                yield return pair.Value;
        }

        static IEnumerable<string> Parts(string sprite)
        {
            yield return sprite + "_stand";
            for (int i = 0; i < ExperimentalTorchModel.FrameCount; i++)
                yield return sprite + (char)('A' + i) + "0_flame";
        }

        [Test]
        public void Every_routed_torch_ships_a_stand_and_all_four_flame_frames()
        {
            foreach (string sprite in Sprites())
                foreach (string part in Parts(sprite))
                {
                    string dir = AssetDir(sprite);
                    Assert.That(File.Exists(Path.Combine(dir, part + ".obj")), Is.True,
                        $"{part}.obj missing — coverage is all-or-nothing, a gap " +
                        "would leave the torch half flat");
                    Assert.That(File.Exists(Path.Combine(dir, part + "_profile.png")),
                        Is.True, $"{part}_profile.png missing");
                    Assert.That(File.Exists(Path.Combine(dir, part + "_spine.png")),
                        Is.True, $"{part}_spine.png missing");
                }
        }

        [Test]
        public void Every_part_resolves_through_Resources()
        {
            // On disk is not the same as loadable: the runtime asks Resources
            // for the prefab and both tables, and a single miss makes the whole
            // torch fall back to the billboard with no error anywhere.
            foreach (string sprite in Sprites())
                foreach (string part in Parts(sprite))
                {
                    string path = "ExperimentalTorches/" + sprite + "/" + part;
                    Assert.That(Resources.Load<GameObject>(path), Is.Not.Null,
                        $"{path} does not load as a prefab");
                    Assert.That(Resources.Load<Texture2D>(path + "_profile"),
                        Is.Not.Null, $"{path}_profile does not load");
                    Assert.That(Resources.Load<Texture2D>(path + "_spine"),
                        Is.Not.Null, $"{path}_spine does not load");
                }
            Assert.That(
                Resources.Load<Shader>("ExperimentalTorches/DoomExperimentalTorch"),
                Is.Not.Null, "the torch shader must live under Resources");
        }

        [Test]
        public void Each_part_resolves_its_material_and_colour_table()
        {
            foreach (string sprite in Sprites())
                foreach (string part in Parts(sprite))
                {
                    string dir = AssetDir(sprite);
                    string mtlRef = FirstToken(Path.Combine(dir, part + ".obj"), "mtllib ");
                    Assert.That(mtlRef, Is.EqualTo(part + ".mtl"),
                        "a dangling mtllib imports the part with Unity's default " +
                        "material and no texture at all");

                    string mapRef = FirstToken(Path.Combine(dir, mtlRef), "map_Kd ");
                    Assert.That(mapRef, Is.EqualTo(part + "_profile.png"));
                    Assert.That(File.Exists(Path.Combine(dir, mapRef)), Is.True);
                }
        }

        [Test]
        public void Generated_stands_resolve_their_material_chain()
        {
            // The TRELLIS stand is not computed, so nothing else checks it.
            // doomify writes <lump>.obj.mtl while the OBJ references
            // <lump>.mtl — install that mismatch and the torch imports with
            // Unity's default material, which renders perfectly white and
            // trips no other assertion (the POSS corpse failure, 2026-08-16).
            foreach (string sprite in Sprites())
            {
                string dir = AssetDir(sprite);
                string lump = sprite + "_stand_mesh";
                if (!File.Exists(Path.Combine(dir, lump + ".obj"))) continue;

                string mtlRef = FirstToken(Path.Combine(dir, lump + ".obj"), "mtllib ");
                Assert.That(mtlRef, Is.EqualTo(lump + ".mtl"));
                string mapRef = FirstToken(Path.Combine(dir, mtlRef), "map_Kd ");
                Assert.That(mapRef, Is.EqualTo(lump + "_albedo.png"));
                Assert.That(File.Exists(Path.Combine(dir, mapRef)), Is.True);

                string resource = "ExperimentalTorches/" + sprite + "/" + lump;
                Assert.That(Resources.Load<GameObject>(resource), Is.Not.Null,
                    $"{resource} does not load as a prefab");
                Assert.That(ExperimentalTorchModel.HasGeneratedStand(sprite), Is.True);
            }
        }

        [Test]
        public void Parts_stand_on_the_origin_and_are_normalized_to_unit_height()
        {
            foreach (string sprite in Sprites())
                foreach (string part in Parts(sprite))
                {
                    var verts = ReadVertices(Path.Combine(AssetDir(sprite), part + ".obj"));
                    Assert.That(verts.Count, Is.GreaterThan(0), $"{part}.obj has no vertices");

                    float minY = float.MaxValue, maxY = float.MinValue;
                    float maxAbsXZ = 0f;
                    foreach (var v in verts)
                    {
                        minY = Mathf.Min(minY, v.y);
                        maxY = Mathf.Max(maxY, v.y);
                        maxAbsXZ = Mathf.Max(maxAbsXZ, Mathf.Abs(v.x), Mathf.Abs(v.z));
                    }

                    // The runtime sets localScale = row count * worldScale and
                    // localPosition = the part's bottom, and never measures.
                    Assert.That(minY, Is.EqualTo(0f).Within(0.001f),
                        $"{part} must stand on y=0");
                    Assert.That(maxY, Is.LessThanOrEqualTo(1.001f),
                        $"{part} must fit in a unit height");
                    Assert.That(maxY, Is.GreaterThan(0.5f),
                        $"{part} is far shorter than its row count claims");
                    Assert.That(maxAbsXZ, Is.LessThan(1f),
                        $"{part} is wider than it is tall — the axis is misplaced");
                }
        }

        [Test]
        public void Uvs_carry_the_row_radius_and_the_height()
        {
            foreach (string sprite in Sprites())
                foreach (string part in Parts(sprite))
                {
                    var uvs = ReadUvs(Path.Combine(AssetDir(sprite), part + ".obj"));
                    Assert.That(uvs.Count, Is.GreaterThan(0), $"{part}.obj has no UVs");

                    var radii = new HashSet<float>();
                    foreach (var uv in uvs)
                    {
                        // U is the row's radius in object units — the shader
                        // divides the projected distance by it. Zero would make
                        // the whole row read as the table's outermost colour.
                        Assert.That(uv.x, Is.GreaterThan(0f).And.LessThan(1f));
                        Assert.That(uv.y, Is.GreaterThanOrEqualTo(0f)
                            .And.LessThanOrEqualTo(1.001f));
                        radii.Add(uv.x);
                    }
                    Assert.That(radii.Count, Is.GreaterThan(1),
                        $"{part} has one radius for every row — a cylinder, not " +
                        "a turned stand or a tapering flame");
                }
        }

        [Test]
        public void Colour_tables_split_the_patch_exactly_between_flame_and_stand()
        {
            string path = WadPath();
            if (!File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(path);

            foreach (string sprite in Sprites())
            {
                var header = Patch.ReadHeader(wad.ReadLump(sprite + "A0"));
                string dir = AssetDir(sprite);
                int standRows = PngHeight(Path.Combine(dir, sprite + "_stand_profile.png"));
                int flameRows = PngHeight(Path.Combine(dir, sprite + "A0_flame_profile.png"));

                // The runtime derives the split from the flame table's height
                // and the patch, so the two must add up to the whole sprite.
                Assert.That(standRows + flameRows, Is.EqualTo(header.Height),
                    $"{sprite}: {flameRows} flame rows + {standRows} stand rows " +
                    $"must cover the patch's {header.Height}");
                Assert.That(header.TopOffset, Is.EqualTo(header.Height),
                    $"{sprite} hangs from a top offset other than its height — " +
                    "the parts would float off the floor");

                for (int i = 1; i < ExperimentalTorchModel.FrameCount; i++)
                {
                    string lump = sprite + (char)('A' + i) + "0";
                    Assert.That(
                        PngHeight(Path.Combine(dir, lump + "_flame_profile.png")),
                        Is.EqualTo(flameRows),
                        $"{lump} splits the sprite at a different row than A0");
                }
            }
        }

        [Test]
        public void Flame_flicker_matches_the_vanilla_state_cadence()
        {
            foreach (var pair in ExperimentalTorchModel.RoutedForTest)
            {
                Assert.That(DecorationAnimationTable.TryGet(pair.Key, out var animation),
                    Is.True, $"thing {pair.Key} ({pair.Value}) has no billboard " +
                    "animation — Classic would stand still while 3D flickers");
                Assert.That(animation.Frames.Length,
                    Is.EqualTo(ExperimentalTorchModel.FrameCount));
                foreach (int tics in animation.Tics)
                    Assert.That(tics, Is.EqualTo(ExperimentalTorchModel.FrameTics));
            }

            // Phase is derived from the level tic in both paths.
            Assert.That(ExperimentalTorchModel.FrameForTic(0), Is.EqualTo(0));
            Assert.That(ExperimentalTorchModel.FrameForTic(3), Is.EqualTo(0));
            Assert.That(ExperimentalTorchModel.FrameForTic(4), Is.EqualTo(1));
            Assert.That(ExperimentalTorchModel.FrameForTic(15), Is.EqualTo(3));
            Assert.That(ExperimentalTorchModel.FrameForTic(16), Is.EqualTo(0));
            Assert.That(ExperimentalTorchModel.FrameForTic(-1), Is.EqualTo(3));
        }

        [Test]
        public void Routed_torches_are_the_things_the_wad_actually_places()
        {
            string path = WadPath();
            if (!File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(path);

            foreach (var pair in ExperimentalTorchModel.RoutedForTest)
            {
                Assert.That(ThingTable.TryGet(pair.Key, out var def), Is.True);
                Assert.That(def.Sprite, Is.EqualTo(pair.Value),
                    $"thing {pair.Key} draws {def.Sprite}, not {pair.Value}");
                for (int i = 0; i < ExperimentalTorchModel.FrameCount; i++)
                {
                    string lump = pair.Value + (char)('A' + i) + "0";
                    Assert.That(wad.FindLump(lump), Is.GreaterThanOrEqualTo(0),
                        $"{lump} must exist in freedoom1.wad");
                }
            }
        }

        [Test]
        public void Candelabra_ships_three_caged_fires_and_their_anchors()
        {
            // CBRA is the torches' odd cousin: its fires sit inside steel
            // cages, so the metal is generated whole and only the fires are
            // computed — three of them, each with its own spine, placed by the
            // table the generator writes beside the meshes.
            string dir = AssetDir("CBRA");
            string table = Path.Combine(dir, "CBRA_fires.txt");
            Assert.That(File.Exists(table), Is.True, "CBRA_fires.txt missing");

            int count = 0;
            foreach (string raw in File.ReadAllLines(table))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string[] fields = line.Split(' ');
                Assert.That(fields.Length, Is.EqualTo(4),
                    "each line is <name> <offsetX> <bottomY> <rows>");
                count++;

                string name = fields[0];
                Assert.That(File.Exists(Path.Combine(dir, name + ".obj")), Is.True);
                Assert.That(File.Exists(Path.Combine(dir, name + "_profile.png")), Is.True);
                Assert.That(File.Exists(Path.Combine(dir, name + "_spine.png")), Is.True);
                Assert.That(FirstToken(Path.Combine(dir, name + ".obj"), "mtllib "),
                    Is.EqualTo(name + ".mtl"));

                string resource = "ExperimentalTorches/CBRA/" + name;
                Assert.That(Resources.Load<GameObject>(resource), Is.Not.Null, resource);
                Assert.That(Resources.Load<Texture2D>(resource + "_profile"),
                    Is.Not.Null, resource + "_profile");
                Assert.That(Resources.Load<Texture2D>(resource + "_spine"),
                    Is.Not.Null, resource + "_spine");

                // Anchors live in patch pixels; a fire outside the sprite would
                // hang in the air next to the candelabra.
                float offsetX = float.Parse(fields[1], CultureInfo.InvariantCulture);
                float bottomY = float.Parse(fields[2], CultureInfo.InvariantCulture);
                float rows = float.Parse(fields[3], CultureInfo.InvariantCulture);
                Assert.That(Mathf.Abs(offsetX), Is.LessThan(16f));
                Assert.That(bottomY, Is.GreaterThan(0f).And.LessThan(61f));
                Assert.That(rows, Is.GreaterThan(1f).And.LessThan(61f));
            }
            Assert.That(count, Is.EqualTo(3), "the sprite draws three lanterns");
            Assert.That(
                Resources.Load<TextAsset>("ExperimentalTorches/CBRA/CBRA_fires"),
                Is.Not.Null, "the anchor table must load as a TextAsset");
        }

        [Test]
        public void Candelabra_fires_sit_inside_the_patch_the_sprite_draws()
        {
            string path = WadPath();
            if (!File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(path);
            var header = Patch.ReadHeader(wad.ReadLump("CBRAA0"));

            // The runtime hangs everything off the patch, so the anchors and
            // the patch must agree: fires inside the picture, metal as tall as
            // the whole sprite.
            Assert.That(header.TopOffset, Is.EqualTo(header.Height));
            foreach (string raw in File.ReadAllLines(
                         Path.Combine(AssetDir("CBRA"), "CBRA_fires.txt")))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string[] fields = line.Split(' ');
                float offsetX = float.Parse(fields[1], CultureInfo.InvariantCulture);
                float bottomY = float.Parse(fields[2], CultureInfo.InvariantCulture);
                float rows = float.Parse(fields[3], CultureInfo.InvariantCulture);
                Assert.That(Mathf.Abs(offsetX),
                    Is.LessThanOrEqualTo(header.Width * 0.5f + 1f));
                Assert.That(bottomY + rows, Is.LessThanOrEqualTo(header.Height));
            }
        }

        [Test]
        public void Candle_is_computed_whole_and_needs_nothing_generated()
        {
            // CAND is a torch in miniature: wax cylinder plus one small flame,
            // both solids of revolution, so it ships with no Space run at all.
            // Its fire never animates — vanilla gives it a single frame.
            string dir = AssetDir("CAND");
            foreach (string part in new[] { "CAND_stand", "CAND_fire0" })
            {
                Assert.That(File.Exists(Path.Combine(dir, part + ".obj")), Is.True,
                    $"{part}.obj missing");
                Assert.That(FirstToken(Path.Combine(dir, part + ".obj"), "mtllib "),
                    Is.EqualTo(part + ".mtl"));
                string resource = "ExperimentalTorches/CAND/" + part;
                Assert.That(Resources.Load<GameObject>(resource), Is.Not.Null, resource);
                Assert.That(Resources.Load<Texture2D>(resource + "_profile"),
                    Is.Not.Null, resource + "_profile");
                Assert.That(Resources.Load<Texture2D>(resource + "_spine"),
                    Is.Not.Null, resource + "_spine");
            }
            Assert.That(File.Exists(Path.Combine(dir, "CAND_stand_mesh.obj")), Is.False,
                "a candle is a cylinder — generating one would be busywork");
            Assert.That(ExperimentalStaticFireModel.HasBody(34), Is.True,
                "the computed wax must satisfy the body requirement");

            string path = WadPath();
            if (!File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(path);
            var header = Patch.ReadHeader(wad.ReadLump("CANDA0"));
            int waxRows = PngHeight(Path.Combine(dir, "CAND_stand_profile.png"));
            int flameRows = PngHeight(Path.Combine(dir, "CAND_fire0_profile.png"));
            Assert.That(waxRows + flameRows, Is.EqualTo(header.Height),
                "wax and flame together must cover the whole patch");

            string[] fields = File.ReadAllLines(Path.Combine(dir, "CAND_fires.txt"))[0]
                .Trim().Split(' ');
            Assert.That(fields.Length, Is.EqualTo(4));
            Assert.That(float.Parse(fields[2], CultureInfo.InvariantCulture),
                Is.EqualTo(waxRows).Within(0.001f),
                "the flame must start exactly where the wax ends");
            Assert.That(float.Parse(fields[3], CultureInfo.InvariantCulture),
                Is.EqualTo(flameRows).Within(0.001f));
        }

        static string FirstToken(string file, string prefix)
        {
            Assert.That(File.Exists(file), Is.True, $"{file} missing");
            foreach (string line in File.ReadLines(file))
                if (line.StartsWith(prefix))
                    return line.Substring(prefix.Length).Trim();
            return null;
        }

        /// PNG header read straight off disk: EditMode tests must not depend on
        /// the texture importer having run.
        static int PngHeight(string file)
        {
            Assert.That(File.Exists(file), Is.True, $"{file} missing");
            using var stream = File.OpenRead(file);
            var head = new byte[24];
            Assert.That(stream.Read(head, 0, head.Length), Is.EqualTo(head.Length));
            return (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        }

        static List<Vector3> ReadVertices(string obj)
        {
            var result = new List<Vector3>();
            foreach (string line in File.ReadLines(obj))
            {
                if (!line.StartsWith("v ")) continue;
                string[] p = line.Split(' ');
                result.Add(new Vector3(Num(p[1]), Num(p[2]), Num(p[3])));
            }
            return result;
        }

        static List<Vector2> ReadUvs(string obj)
        {
            var result = new List<Vector2>();
            foreach (string line in File.ReadLines(obj))
            {
                if (!line.StartsWith("vt ")) continue;
                string[] p = line.Split(' ');
                result.Add(new Vector2(Num(p[1]), Num(p[2])));
            }
            return result;
        }

        static float Num(string s) =>
            float.Parse(s, CultureInfo.InvariantCulture);
    }
}
