using NUnit.Framework;
using Doom.Game;
using Doom.MapBuild;

namespace Doom.Game.Tests
{
    public class SettingsStoreTests
    {
        [Test]
        public void Save_Load_round_trips_v3_including_graphics_mode_and_3d_objects()
        {
            var memory = new MemorySettingsStorage();
            var store = new SettingsStore(memory);
            var data = GameSettingsData.Defaults
                .WithSfxVolume(0.3f)
                .WithMusicVolume(0.4f)
                .WithMouseSensitivity(0.22f)
                .WithInvertY(true)
                .WithFullscreen(false)
                .WithResolution(1280, 720)
                .WithGraphicsMode(GraphicsMode.Enhanced);

            store.Save(data);
            var loaded = store.Load();

            Assert.That(loaded, Is.EqualTo(data));
            Assert.That(memory.GetInt("Doom.Settings.v1.Version", 0),
                Is.EqualTo(GameSettingsData.SchemaVersion));
            Assert.That(memory.GetInt("Doom.Settings.v1.GraphicsMode", -1),
                Is.EqualTo((int)GraphicsMode.Enhanced));
            // v4 stores no Enhanced3DObjects key (mode removed 2026-08-28).
            Assert.That(memory.GetInt("Doom.Settings.v1.Enhanced3DObjects", -1),
                Is.EqualTo(-1));
        }

        [Test]
        public void Load_migrates_v1_prefs_to_Classic_graphics_mode()
        {
            var memory = new MemorySettingsStorage();
            memory.SetInt("Doom.Settings.v1.Version", 1);
            memory.SetFloat("Doom.Settings.v1.SfxVolume", 0.25f);
            memory.SetFloat("Doom.Settings.v1.MusicVolume", 0.35f);
            memory.SetFloat("Doom.Settings.v1.MouseSensitivity", 0.15f);
            memory.SetInt("Doom.Settings.v1.InvertY", 1);
            memory.SetInt("Doom.Settings.v1.Fullscreen", 0);
            memory.SetInt("Doom.Settings.v1.ResW", 800);
            memory.SetInt("Doom.Settings.v1.ResH", 600);
            // No GraphicsMode key — v1.

            var loaded = new SettingsStore(memory).Load();
            Assert.That(loaded.SfxVolume, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(loaded.MusicVolume, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(loaded.MouseSensitivity, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(loaded.InvertY, Is.True);
            Assert.That(loaded.Fullscreen, Is.False);
            Assert.That(loaded.ResolutionWidth, Is.EqualTo(800));
            Assert.That(loaded.ResolutionHeight, Is.EqualTo(600));
            Assert.That(loaded.GraphicsMode, Is.EqualTo(GraphicsMode.Classic));
        }

        /// A v3 store carries the Enhanced3DObjects key of the removed
        /// Enhanced 2D mode — loading must simply ignore it, whatever it
        /// says, and keep the rest of the preferences.
        [Test]
        public void Load_ignores_the_v3_enhanced_2d_key()
        {
            var memory = new MemorySettingsStorage();
            memory.SetInt("Doom.Settings.v1.Version", 3);
            memory.SetFloat("Doom.Settings.v1.SfxVolume", 0.3f);
            memory.SetFloat("Doom.Settings.v1.MusicVolume", 0.55f);
            memory.SetFloat("Doom.Settings.v1.MouseSensitivity", 0.1f);
            memory.SetInt("Doom.Settings.v1.InvertY", 0);
            memory.SetInt("Doom.Settings.v1.Fullscreen", 1);
            memory.SetInt("Doom.Settings.v1.ResW", 0);
            memory.SetInt("Doom.Settings.v1.ResH", 0);
            memory.SetInt("Doom.Settings.v1.GraphicsMode", (int)GraphicsMode.Enhanced);
            memory.SetInt("Doom.Settings.v1.Enhanced3DObjects", 0);

            var loaded = new SettingsStore(memory).Load();
            Assert.That(loaded.GraphicsMode, Is.EqualTo(GraphicsMode.Enhanced));
            Assert.That(loaded.SfxVolume, Is.EqualTo(0.3f).Within(0.001f));
        }

        [Test]
        public void Load_unknown_graphics_mode_int_falls_back_to_Classic()
        {
            var memory = new MemorySettingsStorage();
            memory.SetInt("Doom.Settings.v1.Version", 2);
            memory.SetFloat("Doom.Settings.v1.SfxVolume", 1f);
            memory.SetFloat("Doom.Settings.v1.MusicVolume", 0.55f);
            memory.SetFloat("Doom.Settings.v1.MouseSensitivity", 0.1f);
            memory.SetInt("Doom.Settings.v1.InvertY", 0);
            memory.SetInt("Doom.Settings.v1.Fullscreen", 1);
            memory.SetInt("Doom.Settings.v1.ResW", 0);
            memory.SetInt("Doom.Settings.v1.ResH", 0);
            memory.SetInt("Doom.Settings.v1.GraphicsMode", 99);

            var loaded = new SettingsStore(memory).Load();
            Assert.That(loaded.GraphicsMode, Is.EqualTo(GraphicsMode.Classic));
        }

        [Test]
        public void Load_unsupported_schema_version_returns_defaults()
        {
            var memory = new MemorySettingsStorage();
            memory.SetInt("Doom.Settings.v1.Version", 99);
            memory.SetFloat("Doom.Settings.v1.SfxVolume", 0.1f);

            var loaded = new SettingsStore(memory).Load();
            Assert.That(loaded, Is.EqualTo(GameSettingsData.Defaults));
        }
    }
}
