using System;
using NUnit.Framework;
using Doom.Graphics;
using Doom.MapBuild.Rendering;

namespace Doom.Map.Tests
{
    public class EnhancedVariantStoreTests
    {
        [SetUp]
        public void SetUp() => EnhancedVariantStore.ResetForTests();

        [TearDown]
        public void TearDown() => EnhancedVariantStore.ResetForTests();

        static EnhancedJobResult OkSprite(string id = "PISGA0")
        {
            var rgba = new byte[4 * 4 * 4];
            for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i & 0xff);
            return EnhancedJobResult.OkRgba(
                EnhancedJobKind.Sprite, new DecodedImage(4, 4, rgba));
        }

        static EnhancedLayerConfig Layers(
            bool dedither = true, bool world = true, bool sprites = true, bool ui = true) =>
            new EnhancedLayerConfig(dedither, world, sprites, ui);

        [Test]
        public void Publish_then_lookup_hits()
        {
            var store = EnhancedVariantStore.Instance;
            store.BindWadIdentity("wad:test");
            var layers = Layers();
            var result = OkSprite();

            store.Publish(EnhancedJobKind.Sprite, "10", layers, result);

            Assert.AreEqual(1, store.Count);
            Assert.IsTrue(store.TryGet(EnhancedJobKind.Sprite, "10", layers, out var hit));
            Assert.AreSame(result, hit);
            Assert.That(store.ApproximateCpuBytes, Is.GreaterThan(0L));
        }

        [Test]
        public void Lookup_without_bind_misses()
        {
            var store = EnhancedVariantStore.Instance;
            Assert.IsFalse(store.TryGet(
                EnhancedJobKind.Sprite, "10", Layers(), out _));
        }

        [Test]
        public void Wrong_wadIdentity_misses_and_bind_clears()
        {
            var store = EnhancedVariantStore.Instance;
            store.BindWadIdentity("wad:a");
            store.Publish(EnhancedJobKind.Hud, "STBAR", Layers(), OkSprite());
            Assert.AreEqual(1, store.Count);

            // Exact key under a different wad misses.
            var foreign = new EnhancedVariantKey(
                "wad:b", EnhancedJobKind.Hud, "STBAR", Layers(), EnhancedPipelineVersion.Value);
            Assert.IsFalse(store.TryGetExact(foreign, out _));

            // Bind to a different WAD clears the store.
            store.BindWadIdentity("wad:b");
            Assert.AreEqual(0, store.Count);
            Assert.IsFalse(store.TryGet(EnhancedJobKind.Hud, "STBAR", Layers(), out _));
        }

        [Test]
        public void Wrong_layerConfig_misses()
        {
            var store = EnhancedVariantStore.Instance;
            store.BindWadIdentity("wad:test");
            store.Publish(EnhancedJobKind.Sprite, "10", Layers(dedither: true), OkSprite());

            Assert.IsFalse(store.TryGet(
                EnhancedJobKind.Sprite, "10", Layers(dedither: false), out _));
            Assert.IsTrue(store.TryGet(
                EnhancedJobKind.Sprite, "10", Layers(dedither: true), out _));
        }

        [Test]
        public void Wrong_pipelineVersion_misses()
        {
            var store = EnhancedVariantStore.Instance;
            store.BindWadIdentity("wad:test");
            var layers = Layers();
            var stale = new EnhancedVariantKey(
                "wad:test", EnhancedJobKind.Sprite, "10", layers,
                pipelineVersion: EnhancedPipelineVersion.Value + 1);
            store.PublishExact(stale, OkSprite());

            Assert.IsFalse(store.TryGet(EnhancedJobKind.Sprite, "10", layers, out _),
                "current pipeline version must not serve a stale key");
            Assert.IsTrue(store.TryGetExact(stale, out _));
        }

        [Test]
        public void Failed_result_is_not_published()
        {
            var store = EnhancedVariantStore.Instance;
            store.BindWadIdentity("wad:test");
            store.Publish(
                EnhancedJobKind.Sprite, "10", Layers(),
                EnhancedJobResult.Failed(EnhancedJobKind.Sprite, "boom"));
            Assert.AreEqual(0, store.Count);
        }

        [Test]
        public void Lazy_build_publishes_under_active_profile_layers()
        {
            string wadPath = System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "StreamingAssets", "wads", "freedoom1.wad");
            if (!System.IO.File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            var store = EnhancedVariantStore.Instance;
            store.BindWadIdentity("wad:layer-derivation");

            using var wad = Doom.Wad.WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);

            var factory = new DoomMaterialFactory();
            var custom = GraphicsProfile.EnhancedWithLayers(worldDedither: false);
            factory.SetActiveProfile(custom);
            var cache = new Doom.MapBuild.TextureCache(wad, textures, palette, factory);

            cache.GetTexture("FLOOR0_1", WorldTextureVariant.Enhanced4X);

            var customLayers = EnhancedLayerConfig.FromProfile(custom);
            Assert.IsTrue(
                store.TryGet(EnhancedJobKind.WorldAlbedo, "FLOOR0_1", customLayers, out _),
                "lazy build must publish under the layers it was actually built with");

            var enhancedLayers = EnhancedLayerConfig.FromProfile(GraphicsProfile.Enhanced);
            Assert.IsFalse(
                store.TryGet(EnhancedJobKind.WorldAlbedo, "FLOOR0_1", enhancedLayers, out _),
                "dedither-off content must not be served under full Enhanced layers");
        }

        [Test]
        public void LayerConfig_from_Enhanced_profile_matches()
        {
            var fromProfile = EnhancedLayerConfig.FromProfile(GraphicsProfile.Enhanced);
            Assert.IsTrue(fromProfile.WorldDedither);
            Assert.IsTrue(fromProfile.WorldUpscale4X);
            Assert.IsTrue(fromProfile.SpritesUpscale4X);
            Assert.IsTrue(fromProfile.UiUpscale4X);

            var classic = EnhancedLayerConfig.FromProfile(GraphicsProfile.Classic);
            Assert.AreNotEqual(fromProfile, classic);
        }
    }
}
