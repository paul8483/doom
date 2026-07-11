using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Graphics.Tests
{
    public class TextureAnimationCatalogTests
    {
        [Test]
        public void Complete_range_resolves_all_frames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal)
            {
                "NUKAGE1", "NUKAGE2", "NUKAGE3",
            };
            var catalog = TextureAnimationCatalog.Build(names.Contains);

            Assert.IsTrue(catalog.TryGet("NUKAGE1", out var seq));
            Assert.IsTrue(seq.IsValid);
            Assert.AreEqual(3, seq.Frames.Length);
            Assert.AreEqual("NUKAGE1", seq.Frames[0]);
            Assert.AreEqual("NUKAGE3", seq.Frames[2]);
            Assert.AreEqual(8, seq.TicDuration);
            Assert.IsFalse(seq.IsWall);

            Assert.IsTrue(catalog.TryGet("nukage2", out var mid));
            Assert.AreEqual(seq.BaseName, mid.BaseName);
        }

        [Test]
        public void Missing_middle_frame_truncates_sequence()
        {
            // LAVA3 missing → keep LAVA1/LAVA2 only; LAVA4 is not glued across a hole.
            bool Exists(string n) => n == "LAVA1" || n == "LAVA2" || n == "LAVA4";
            var catalog = TextureAnimationCatalog.Build(Exists);

            Assert.IsTrue(catalog.TryGet("LAVA1", out var seq));
            Assert.AreEqual(2, seq.Frames.Length);
            Assert.AreEqual("LAVA1", seq.Frames[0]);
            Assert.AreEqual("LAVA2", seq.Frames[1]);
            Assert.IsFalse(catalog.TryGet("LAVA4", out _));
        }

        [Test]
        public void Single_present_frame_disables_sequence()
        {
            var names = new HashSet<string>(StringComparer.Ordinal) { "BLOOD1" };
            var catalog = TextureAnimationCatalog.Build(names.Contains);
            Assert.IsFalse(catalog.TryGet("BLOOD1", out _));
            Assert.AreEqual(0, catalog.SequenceCount);
        }

        [Test]
        public void Unknown_name_is_not_animated()
        {
            var names = new HashSet<string>(StringComparer.Ordinal)
            {
                "NUKAGE1", "NUKAGE2", "NUKAGE3", "FLAT1",
            };
            var catalog = TextureAnimationCatalog.Build(names.Contains);
            Assert.IsFalse(catalog.TryGet("FLAT1", out _));
            Assert.IsFalse(catalog.TryGet("MISSING", out _));
        }

        [Test]
        public void Wall_range_marks_IsWall()
        {
            var names = new HashSet<string>(StringComparer.Ordinal)
            {
                "SFALL1", "SFALL2", "SFALL3", "SFALL4",
            };
            var catalog = TextureAnimationCatalog.Build(names.Contains);
            Assert.IsTrue(catalog.TryGet("SFALL1", out var seq));
            Assert.IsTrue(seq.IsWall);
            Assert.AreEqual(4, seq.Frames.Length);
        }

        [Test]
        public void IncrementName_walks_numeric_suffix()
        {
            Assert.AreEqual("NUKAGE2", TextureAnimationCatalog.IncrementName("NUKAGE1"));
            Assert.AreEqual("SLIME02", TextureAnimationCatalog.IncrementName("SLIME01"));
            Assert.AreEqual("RROCK06", TextureAnimationCatalog.IncrementName("RROCK05"));
        }

        [Test]
        public void Freedoom_E1_fluids_resolve_when_WAD_present()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(path))
                Assert.Ignore("freedoom1.wad not found");

            using var wad = WadFile.Open(path);
            var textures = TextureSet.Load(wad);
            bool Exists(string name)
            {
                if (textures.Contains(name)) return true;
                int idx = wad.FindLump(name);
                if (idx < 0) return false;
                return wad.Directory[idx].Size == 64 * 64;
            }

            var catalog = TextureAnimationCatalog.Build(Exists);

            Assert.IsTrue(catalog.TryGet("NUKAGE1", out var nukage), "NUKAGE1 sequence");
            Assert.That(nukage.Frames.Length, Is.GreaterThanOrEqualTo(2));

            Assert.IsTrue(catalog.TryGet("LAVA1", out var lava), "LAVA1 sequence");
            Assert.That(lava.Frames.Length, Is.GreaterThanOrEqualTo(2));

            // At least one fluid sequence must exist for E1 atmosphere.
            Assert.That(catalog.SequenceCount, Is.GreaterThan(0));
        }
    }
}
