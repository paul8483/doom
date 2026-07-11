using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Graphics.Tests
{
    /// Pins Freedoom Phase 1 lumps required by the plasma/BFG Stage 6c extension.
    public class PlasmaBfgWadTests
    {
        static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void Plasma_and_BFG_viewmodel_projectile_and_effect_frames_resolve()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var set = SpriteSet.Load(wad);

            foreach (var (sprite, frames) in new (string, int[])[]
            {
                ("PLSG", new[] { 0, 1 }),
                ("PLSF", new[] { 0, 1 }),
                ("PLSS", new[] { 0, 1 }),
                ("PLSE", new[] { 0, 1, 2, 3, 4 }),
                ("BFGG", new[] { 0, 1 }),
                ("BFGF", new[] { 0, 1 }),
                ("BFS1", new[] { 0, 1 }),
                ("BFE1", new[] { 0, 1, 2, 3, 4, 5 }),
                ("BFE2", new[] { 0, 1, 2, 3 }),
                ("PLAS", new[] { 0 }),
                ("BFUG", new[] { 0 }),
                ("CELL", new[] { 0 }),
                ("CELP", new[] { 0 }),
            })
            {
                foreach (int frame in frames)
                    Assert.That(set.TryGet(sprite, frame, 0, out _), Is.True,
                        $"{sprite} frame {frame}");
            }
        }

        [Test]
        public void Plasma_and_BFG_sfx_lumps_exist()
        {
            using var wad = WadFile.Open(FreedoomPath);
            foreach (string name in new[]
            {
                "DSPLASMA", "DSBFG", "DSFIRXPL", "DSRXPLOD", "DSWPNUP", "DSITEMUP",
            })
            {
                int idx = wad.FindLump(name);
                Assert.That(idx, Is.GreaterThanOrEqualTo(0), name);
                Assert.That(wad.ReadLump(idx).Length, Is.GreaterThan(0), name);
            }
        }
    }
}
