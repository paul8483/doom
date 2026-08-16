using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Graphics;
using Doom.MapBuild;
using Doom.Things;
using Doom.Wad;

namespace Doom.Map.Tests
{
    /// The stop-motion mesh table is indexed by the BRAIN's frame number, so a
    /// drifted table would silently show the wrong pose (or revert to the
    /// billboard mid-animation). These pin it to the gameplay tables and to
    /// the WAD patch headers the billboard renders at.
    public class MonsterModelFrameTableTests
    {
        static readonly (int DoomEdNum, string Sprite)[] Routed =
        {
            (3004, "POSS"), (9, "SPOS"), (3001, "TROO"),
            (3002, "SARG"), (3003, "BOSS"),
        };

        static string WadPath() => Path.Combine(
            Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void Table_declares_the_whole_death_chain_including_the_corpse()
        {
            foreach (var (num, sprite) in Routed)
            {
                Assert.That(ExperimentalMonsterModel.TryGetFrameTableForTest(
                    sprite, out int live, out var lumps, out var heights),
                    Is.True, $"{sprite} must be routed");
                Assert.That(heights.Length, Is.EqualTo(lumps.Length),
                    $"{sprite}: one patch height per frame");
                Assert.That(MonsterTable.TryGet(num, out var mon), Is.True);
                Assert.That(ThingTable.TryGet(num, out var thing), Is.True);

                int[] death = mon.Death.Frames;
                Assert.That(death[0], Is.EqualTo(live),
                    $"{sprite}: the death sequence must start right after the " +
                    "live frames — a gap would revert to the billboard");
                for (int i = 1; i < death.Length; i++)
                    Assert.That(death[i], Is.EqualTo(death[i - 1] + 1),
                        $"{sprite}: death frames must be contiguous");

                Assert.That(thing.CorpseFrame, Is.GreaterThanOrEqualTo(live),
                    $"{sprite}: the corpse frame follows the live frames");
                Assert.That(lumps.Length,
                    Is.EqualTo(Mathf.Max(death[death.Length - 1],
                                         thing.CorpseFrame) + 1),
                    $"{sprite}: the table must declare the death sequence and " +
                    "the corpse — how far 3D actually reaches is decided at " +
                    "attach time by the meshes on disk");
            }
        }

        [Test]
        public void Live_frames_keep_their_rotation_1_lumps()
        {
            foreach (var (_, sprite) in Routed)
            {
                Assert.That(ExperimentalMonsterModel.TryGetFrameTableForTest(
                    sprite, out int live, out var lumps, out _), Is.True);
                for (int i = 0; i < lumps.Length; i++)
                {
                    // Live frames are drawn per rotation, death frames once.
                    string expected = i < live ? "1" : "0";
                    Assert.That(lumps[i].Substring(lumps[i].Length - 1),
                        Is.EqualTo(expected),
                        $"{sprite}{lumps[i]}: frame {i} rotation suffix");
                }
            }
        }

        [Test]
        public void Xdeath_frames_stay_outside_the_mesh_table()
        {
            foreach (var (num, sprite) in Routed)
            {
                Assert.That(ExperimentalMonsterModel.TryGetFrameTableForTest(
                    sprite, out _, out var lumps, out _), Is.True);
                Assert.That(MonsterTable.TryGet(num, out var mon), Is.True);
                if (mon.XDeath == null) continue;
                foreach (int frame in mon.XDeath.Frames)
                    Assert.That(frame, Is.GreaterThanOrEqualTo(lumps.Length),
                        $"{sprite}: gibs must fall out of coverage");
            }
        }

        /// A mesh whose OBJ points at a material file that is not there
        /// imports with Unity's default white material, and nothing else in
        /// the suite notices — the model still attaches, still swaps frames
        /// and still passes every visibility assert. That is exactly how the
        /// POSS death frames shipped as white silhouettes (2026-08-16): the
        /// decimator writes `mtllib ./<lump>.obj.mtl`, the repo keeps
        /// `<lump>.mtl`. Pin the whole chain OBJ -> MTL -> albedo instead.
        [Test]
        public void Every_frame_mesh_resolves_its_material_and_albedo()
        {
            foreach (var (_, sprite) in Routed)
            {
                Assert.That(ExperimentalMonsterModel.TryGetFrameTableForTest(
                    sprite, out _, out var lumps, out _), Is.True);
                string dir = Path.Combine(Application.dataPath, "Resources",
                    "ExperimentalMonsters", sprite);
                foreach (string frame in lumps)
                {
                    string lump = sprite + frame;
                    string obj = Path.Combine(dir, lump + ".obj");
                    // Frames still to be authored simply have no files yet;
                    // coverage is decided at attach time, not here.
                    if (!File.Exists(obj)) continue;

                    string mtlRef = null;
                    foreach (string line in File.ReadLines(obj))
                    {
                        if (!line.StartsWith("mtllib ")) continue;
                        mtlRef = line.Substring(7).Trim();
                        break;
                    }
                    Assert.That(mtlRef, Is.EqualTo(lump + ".mtl"),
                        $"{lump}.obj must name its own material file — a " +
                        "dangling mtllib renders the frame plain white");

                    string mtl = Path.Combine(dir, mtlRef);
                    Assert.That(File.Exists(mtl), Is.True, $"{mtlRef} missing");

                    string mapRef = null;
                    foreach (string line in File.ReadLines(mtl))
                    {
                        if (!line.StartsWith("map_Kd ")) continue;
                        mapRef = line.Substring(7).Trim();
                        break;
                    }
                    Assert.That(mapRef, Is.EqualTo(lump + "_albedo.png"),
                        $"{mtlRef}: albedo must be this frame's own texture");
                    Assert.That(File.Exists(Path.Combine(dir, mapRef)), Is.True,
                        $"{mapRef} missing");
                }
            }
        }

        [Test]
        public void Frame_lumps_and_heights_match_the_wad_patches()
        {
            string path = WadPath();
            if (!File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(path);

            foreach (var (_, sprite) in Routed)
            {
                Assert.That(ExperimentalMonsterModel.TryGetFrameTableForTest(
                    sprite, out _, out var lumps, out var heights), Is.True);
                for (int i = 0; i < lumps.Length; i++)
                {
                    string lump = sprite + lumps[i];
                    Assert.That(wad.FindLump(lump), Is.GreaterThanOrEqualTo(0),
                        $"{lump} must exist in freedoom1.wad");
                    var header = Patch.ReadHeader(wad.ReadLump(lump));
                    Assert.That(heights[i], Is.EqualTo((float)header.Height),
                        $"{lump}: mesh normalization height must equal the " +
                        "native patch height the billboard draws at");
                }
            }
        }
    }
}
