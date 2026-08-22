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
        public void Dead_decorations_route_the_death_chain_corpse_meshes()
        {
            // Map-placed dead monsters (info.c MT_DEAD*) draw the corpse
            // sprite; the seam must hand them the same mesh and the same
            // normalization rule the killed monster's corpse uses.
            foreach (var (num, sprite) in new[]
                     { (18, "POSS"), (19, "SPOS"), (20, "TROO"), (21, "SARG"),
                       (10, "PLAY"), (12, "PLAY") })
            {
                Assert.That(ExperimentalMonsterModel.TryDescribeCorpse(
                        num, out string resource, out float sizePx,
                        out bool byWidth, out string _),
                    Is.True, $"dead thing {num} must route a corpse mesh");
                Assert.That(resource, Does.StartWith("ExperimentalMonsters/" + sprite),
                    $"thing {num} routes {resource}");
                Assert.That(Resources.Load<GameObject>(resource), Is.Not.Null,
                    $"{resource} does not load as a prefab");

                // The size constant stays honest against the WAD patch.
                string lump = resource.Substring(resource.LastIndexOf('/') + 1);
                string path = WadPath();
                if (!File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
                using var wad = WadFile.Open(path);
                int idx = wad.FindLump(lump);
                Assert.That(idx, Is.GreaterThanOrEqualTo(0), $"{lump} not in WAD");
                var header = Patch.ReadHeader(wad.ReadLump(idx));
                Assert.That(sizePx,
                    Is.EqualTo(byWidth ? (float)header.Width : (float)header.Height),
                    $"{lump}: corpse size constant no longer matches the WAD");
            }

            // Dead player (15) has no PLAY mesh and must stay on the sprite.
            Assert.That(ExperimentalMonsterModel.TryDescribeCorpse(
                    15, out _, out _, out _, out _),
                Is.False, "dead player has no mesh to route");
        }

        [Test]
        public void Xdeath_corpses_route_meshes_pinned_to_the_wad()
        {
            // The gib ANIMATION stays on the billboard; the lasting pool of
            // remains (frame U) swaps in a mesh. SARG and BOSS have no XDeath.
            string path = WadPath();
            if (!File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(path);

            foreach (string sprite in new[] { "POSS", "SPOS", "TROO" })
            {
                Assert.That(ExperimentalMonsterModel.TryGetXdeathForTest(
                        sprite, out string lump, out float widthPx),
                    Is.True, $"{sprite} must declare an xdeath corpse");
                Assert.That(Resources.Load<GameObject>(
                        "ExperimentalMonsters/" + sprite + "/" + lump),
                    Is.Not.Null, $"{lump} does not load as a prefab");
                int idx = wad.FindLump(lump);
                Assert.That(idx, Is.GreaterThanOrEqualTo(0), $"{lump} not in WAD");
                Assert.That(widthPx,
                    Is.EqualTo((float)Patch.ReadHeader(wad.ReadLump(idx)).Width),
                    $"{lump}: xdeath width constant no longer matches the WAD");
            }

            foreach (string sprite in new[] { "SARG", "BOSS" })
                Assert.That(ExperimentalMonsterModel.TryGetXdeathForTest(
                        sprite, out _, out _),
                    Is.False, $"{sprite} has no XDeath in info.c");
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

        /// Frames that lie flat on the floor are scaled by the patch WIDTH:
        /// a pile's Y extent is its thickness, and forcing that to the patch
        /// height stands the pile up on its edge (the 2026-08-17 gate, and
        /// again after the tilt was traded for a Y stretch). Pin the widths
        /// to the WAD, and pin the rule that only death frames may use it —
        /// a monster on its feet is measured by its height.
        [Test]
        public void Flat_frames_are_pinned_to_the_native_patch_widths()
        {
            string path = WadPath();
            if (!File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(path);

            foreach (var (_, sprite) in Routed)
            {
                Assert.That(ExperimentalMonsterModel.TryGetFrameTableForTest(
                    sprite, out int live, out var lumps, out _), Is.True);
                Assert.That(ExperimentalMonsterModel.TryGetFlatWidthsForTest(
                    sprite, out var widths), Is.True);
                Assert.That(widths.Length, Is.EqualTo(lumps.Length),
                    $"{sprite}: one flat-width slot per frame");

                for (int i = 0; i < lumps.Length; i++)
                {
                    if (widths[i] <= 0f) continue;
                    Assert.That(i, Is.GreaterThanOrEqualTo(live),
                        $"{sprite}{lumps[i]}: a live frame stands on its feet " +
                        "and must be scaled by height");
                    var header = Patch.ReadHeader(wad.ReadLump(sprite + lumps[i]));
                    Assert.That(widths[i], Is.EqualTo((float)header.Width),
                        $"{sprite}{lumps[i]}: a flat frame is scaled to the " +
                        "native patch width");
                }
            }
        }
    }
}
