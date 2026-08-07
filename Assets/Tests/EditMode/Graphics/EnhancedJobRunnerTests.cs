using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Graphics.Tests
{
    public class EnhancedJobRunnerTests
    {
        static string FreedoomPath => Path.Combine(
            Application.dataPath, "StreamingAssets", "wads", "freedoom1.wad");

        [Test]
        public void Pipeline_version_is_positive()
        {
            Assert.GreaterOrEqual(EnhancedPipelineVersion.Value, 1);
        }

        [Test]
        public void Null_job_throws()
        {
            Assert.Throws<ArgumentNullException>(() => EnhancedJobRunner.Run(null));
        }

        [Test]
        public void Failed_result_on_invalid_native_dimensions()
        {
            var job = EnhancedJob.ForSprite(
                "bad",
                new DecodedImage(0, 1, Array.Empty<byte>()),
                applyDedither: false);
            var result = EnhancedJobRunner.Run(job);
            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.ErrorMessage);
        }

        [Test]
        public void World_albedo_matches_legacy_pipeline_composition()
        {
            using var ctx = OpenFreedoom();
            var native = ctx.Textures.Build("STARTAN2", ctx.Palette);
            var wrap = PixelWrapMode.RepeatX;

            var job = EnhancedJob.ForWorldAlbedo(
                "STARTAN2", native, wrap, applyDedither: true, applyAlphaBleed: false, ctx.Palette);
            var result = EnhancedJobRunner.Run(job);
            Assert.IsTrue(result.Success, result.ErrorMessage);

            var legacy = LegacyWorldAlbedo(native, wrap, applyDedither: true, applyAlphaBleed: false, ctx.Palette);
            AssertMipChainsEqual(legacy, result.AlbedoMips);
        }

        [Test]
        public void Sprite_matches_legacy_pipeline_composition()
        {
            using var ctx = OpenFreedoom();
            int lump = ctx.Wad.FindLump("PISGA0");
            Assert.That(lump, Is.GreaterThanOrEqualTo(0));
            var native = Patch.Decode(ctx.Wad.ReadLump(lump), ctx.Palette);

            var job = EnhancedJob.ForSprite("PISGA0", native, applyDedither: true);
            var result = EnhancedJobRunner.Run(job);
            Assert.IsTrue(result.Success, result.ErrorMessage);

            var legacy = LegacySpriteHud(native, applyDedither: true);
            Assert.AreEqual(legacy.Rgba, result.Rgba.Rgba);
        }

        [Test]
        public void World_normal_matches_legacy_pipeline_composition()
        {
            using var ctx = OpenFreedoom();
            var native = ctx.Textures.Build("STARTAN2", ctx.Palette);
            var wrap = PixelWrapMode.RepeatX;
            var albedo = EnhancedJobRunner.Run(EnhancedJob.ForWorldAlbedo(
                "STARTAN2", native, wrap, true, false, ctx.Palette)).AlbedoMips;
            var category = MaterialSurfaceClassifier.Classify("STARTAN2", isFlat: false);

            var job = EnhancedJob.ForWorldNormal("STARTAN2", albedo, category, wrap);
            var result = EnhancedJobRunner.Run(job);
            Assert.IsTrue(result.Success, result.ErrorMessage);

            var legacy = LegacyWorldNormal(albedo, category, wrap);
            AssertMipChainsEqual(legacy, result.NormalMips);
        }

        [Test]
        public void Parallel_matches_sequential_byte_for_byte_on_freedoom_set()
        {
            using var ctx = OpenFreedoom();
            var jobs = BuildFreedoomJobSet(ctx);
            Assert.GreaterOrEqual(jobs.Count, 10, "expected a diverse Freedoom sample");

            const int repeats = 4;
            EnhancedJobResult[] sequential = null;
            for (int r = 0; r < repeats; r++)
            {
                var pass = RunSequential(jobs);
                if (sequential == null)
                    sequential = pass;
                else
                    AssertResultsEqual(sequential, pass, $"sequential repeat {r}");
            }

            for (int r = 0; r < repeats; r++)
            {
                var parallel = RunParallel(jobs);
                AssertResultsEqual(sequential, parallel, $"parallel repeat {r}");
            }
        }

        [Test]
        public void BuildEnhanced4X_matches_runner_world_level_zero()
        {
            using var ctx = OpenFreedoom();
            var native = Flat.Decode(ctx.Wad.ReadLump(ctx.Wad.FindLump("FLOOR4_8")), ctx.Palette);
            var wrap = PixelWrapMode.RepeatXY;
            var direct = EnhancedJobRunner.BuildEnhanced4X(native, wrap, true, false);
            var viaJob = EnhancedJobRunner.Run(EnhancedJob.ForWorldAlbedo(
                "FLOOR4_8", native, wrap, true, false, ctx.Palette));
            Assert.IsTrue(viaJob.Success, viaJob.ErrorMessage);
            Assert.AreEqual(direct.Rgba, viaJob.AlbedoMips[0].Rgba);
        }

        static List<EnhancedJob> BuildFreedoomJobSet(FreedoomContext ctx)
        {
            var jobs = new List<EnhancedJob>();

            void AddWall(string name, bool bleed)
            {
                Assert.IsTrue(ctx.Textures.Contains(name), name);
                var img = ctx.Textures.Build(name, ctx.Palette);
                jobs.Add(EnhancedJob.ForWorldAlbedo(
                    name, img, PixelWrapMode.RepeatX, true, bleed, ctx.Palette));
            }

            AddWall("STARTAN2", bleed: false);
            AddWall("DOOR3", bleed: false);

            // First masked wall for alpha-bleed path.
            foreach (var name in ctx.Textures.Names)
            {
                var img = ctx.Textures.Build(name, ctx.Palette);
                if (!HasTransparent(img)) continue;
                jobs.Add(EnhancedJob.ForWorldAlbedo(
                    name, img, PixelWrapMode.RepeatX, true, true, ctx.Palette));
                break;
            }

            int flatIdx = ctx.Wad.FindLump("FLOOR4_8");
            Assert.That(flatIdx, Is.GreaterThanOrEqualTo(0));
            var flat = Flat.Decode(ctx.Wad.ReadLump(flatIdx), ctx.Palette);
            jobs.Add(EnhancedJob.ForWorldAlbedo(
                "FLOOR4_8", flat, PixelWrapMode.RepeatXY, true, false, ctx.Palette));

            int skyIdx = ctx.Wad.FindLump("SKY1");
            Assert.That(skyIdx, Is.GreaterThanOrEqualTo(0));
            var sky = Patch.Decode(ctx.Wad.ReadLump(skyIdx), ctx.Palette);
            jobs.Add(EnhancedJob.ForWorldAlbedo(
                "SKY1", sky, PixelWrapMode.RepeatX, true, false, ctx.Palette));

            foreach (var sprite in new[] { "PISGA0", "POSSA1", "MEDIA0" })
            {
                int lump = ctx.Wad.FindLump(sprite);
                Assert.That(lump, Is.GreaterThanOrEqualTo(0), sprite);
                var img = Patch.Decode(ctx.Wad.ReadLump(lump), ctx.Palette);
                jobs.Add(EnhancedJob.ForSprite(sprite, img, applyDedither: true));
            }

            int stbar = ctx.Wad.FindLump("STBAR");
            Assert.That(stbar, Is.GreaterThanOrEqualTo(0));
            var hud = Patch.Decode(ctx.Wad.ReadLump(stbar), ctx.Palette);
            jobs.Add(EnhancedJob.ForHud("STBAR", hud, applyDedither: true));

            // World normals from the first albedo job result (compute albedo first).
            var albedoJob = jobs[0];
            var albedo = EnhancedJobRunner.Run(albedoJob);
            Assert.IsTrue(albedo.Success, albedo.ErrorMessage);
            jobs.Add(EnhancedJob.ForWorldNormal(
                albedoJob.ItemId + "/N",
                albedo.AlbedoMips,
                MaterialSurfaceClassifier.Classify(albedoJob.ItemId, isFlat: false),
                albedoJob.Wrap));

            return jobs;
        }

        static EnhancedJobResult[] RunSequential(IReadOnlyList<EnhancedJob> jobs)
        {
            var results = new EnhancedJobResult[jobs.Count];
            for (int i = 0; i < jobs.Count; i++)
                results[i] = EnhancedJobRunner.Run(jobs[i]);
            return results;
        }

        static EnhancedJobResult[] RunParallel(IReadOnlyList<EnhancedJob> jobs)
        {
            var results = new EnhancedJobResult[jobs.Count];
            Parallel.For(0, jobs.Count, i =>
            {
                results[i] = EnhancedJobRunner.Run(jobs[i]);
            });
            return results;
        }

        static void AssertResultsEqual(
            EnhancedJobResult[] expected, EnhancedJobResult[] actual, string label)
        {
            Assert.AreEqual(expected.Length, actual.Length, label);
            for (int i = 0; i < expected.Length; i++)
            {
                var e = expected[i];
                var a = actual[i];
                Assert.AreEqual(e.Success, a.Success, $"{label}[{i}] success");
                Assert.AreEqual(e.Kind, a.Kind, $"{label}[{i}] kind");
                if (!e.Success)
                {
                    Assert.AreEqual(e.ErrorMessage, a.ErrorMessage, $"{label}[{i}] error");
                    continue;
                }

                switch (e.Kind)
                {
                    case EnhancedJobKind.WorldAlbedo:
                        AssertMipChainsEqual(e.AlbedoMips, a.AlbedoMips, $"{label}[{i}]");
                        break;
                    case EnhancedJobKind.WorldNormal:
                        AssertMipChainsEqual(e.NormalMips, a.NormalMips, $"{label}[{i}]");
                        break;
                    default:
                        Assert.AreEqual(e.Rgba.Rgba, a.Rgba.Rgba, $"{label}[{i}] rgba");
                        break;
                }
            }
        }

        static PaletteMipChain LegacyWorldAlbedo(
            DecodedImage native, PixelWrapMode wrap, bool applyDedither, bool applyAlphaBleed, Palette palette)
        {
            var processed = applyDedither ? DeditherFilter.Apply(native, wrap) : native;
            if (applyAlphaBleed)
                processed = AlphaBleedGuard.Dilate(processed);
            var x2 = SuperXbrUpscaler.Scale2X(processed, wrap);
            var x4 = SuperXbrUpscaler.Scale2X(x2, wrap);
            return PaletteMipGenerator.Generate(x4, palette, wrap, preserveAlphaCoverage: true);
        }

        static DecodedImage LegacySpriteHud(DecodedImage native, bool applyDedither)
        {
            var wrap = PixelWrapMode.Clamp;
            var processed = applyDedither ? DeditherFilter.Apply(native, wrap) : native;
            processed = AlphaBleedGuard.Dilate(processed);
            var x2 = SuperXbrUpscaler.Scale2X(processed, wrap);
            var x4 = SuperXbrUpscaler.Scale2X(x2, wrap);
            return SharpenFilter.Apply(x4);
        }

        static PaletteMipChain LegacyWorldNormal(
            PaletteMipChain albedo, MaterialSurfaceCategory category, PixelWrapMode wrap)
        {
            var profile = MaterialSurfaceProfile.For(category);
            var levels = new DecodedImage[albedo.Count];
            for (int level = 0; level < albedo.Count; level++)
            {
                var height = HeightMapGenerator.Generate(albedo[level], category, wrap);
                levels[level] = NormalMapGenerator.Generate(height, profile.Strength, profile.Wrap);
            }
            return new PaletteMipChain(levels);
        }

        static void AssertMipChainsEqual(PaletteMipChain a, PaletteMipChain b, string label = null)
        {
            Assert.IsNotNull(a, label);
            Assert.IsNotNull(b, label);
            Assert.AreEqual(a.Count, b.Count, label);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Width, b[i].Width, $"{label} level {i} w");
                Assert.AreEqual(a[i].Height, b[i].Height, $"{label} level {i} h");
                Assert.AreEqual(a[i].Rgba, b[i].Rgba, $"{label} level {i} rgba");
            }
        }

        static bool HasTransparent(DecodedImage img)
        {
            for (int i = 3; i < img.Rgba.Length; i += 4)
                if (img.Rgba[i] == 0) return true;
            return false;
        }

        static FreedoomContext OpenFreedoom()
        {
            if (!File.Exists(FreedoomPath))
                Assert.Ignore("freedoom1.wad missing");
            var wad = WadFile.Open(FreedoomPath);
            return new FreedoomContext(wad);
        }

        sealed class FreedoomContext : IDisposable
        {
            public WadFile Wad { get; }
            public Palette Palette { get; }
            public TextureSet Textures { get; }

            public FreedoomContext(WadFile wad)
            {
                Wad = wad;
                Palette = new Palette(wad.ReadLump("PLAYPAL"));
                Textures = TextureSet.Load(wad);
            }

            public void Dispose() => Wad.Dispose();
        }
    }
}
