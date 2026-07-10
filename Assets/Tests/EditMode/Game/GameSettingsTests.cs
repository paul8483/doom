using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class GameSettingsTests
    {
        [Test]
        public void Defaults_match_inspector_baselines()
        {
            var d = GameSettingsData.Defaults;
            Assert.That(d.SfxVolume, Is.EqualTo(1f));
            Assert.That(d.MusicVolume, Is.EqualTo(0.55f));
            Assert.That(d.MouseSensitivity, Is.EqualTo(0.1f));
            Assert.That(d.InvertY, Is.False);
            Assert.That(d.Fullscreen, Is.True);
            Assert.That(d.ResolutionWidth, Is.EqualTo(0));
            Assert.That(d.ResolutionHeight, Is.EqualTo(0));
        }

        [Test]
        public void TryCreate_clamps_volumes_and_sensitivity()
        {
            Assert.That(GameSettingsData.TryCreate(
                2f, -1f, 5f, false, true, 0, 0, out var data, out _), Is.True);
            Assert.That(data.SfxVolume, Is.EqualTo(1f));
            Assert.That(data.MusicVolume, Is.EqualTo(0f));
            Assert.That(data.MouseSensitivity, Is.EqualTo(2f));
        }

        [Test]
        public void TryCreate_rejects_NaN_and_Infinity()
        {
            Assert.That(GameSettingsData.TryCreate(
                float.NaN, 0.5f, 0.1f, false, true, 0, 0, out _, out var err), Is.False);
            Assert.That(err, Does.Contain("finite"));

            Assert.That(GameSettingsData.TryCreate(
                0.5f, float.PositiveInfinity, 0.1f, false, true, 0, 0, out _, out _), Is.False);
        }

        [Test]
        public void TryCreate_rejects_partial_resolution()
        {
            Assert.That(GameSettingsData.TryCreate(
                1f, 0.55f, 0.1f, false, true, 1920, 0, out _, out _), Is.False);
        }

        [Test]
        public void Withers_round_trip_invert_fullscreen_resolution()
        {
            var d = GameSettingsData.Defaults
                .WithInvertY(true)
                .WithFullscreen(false)
                .WithResolution(1280, 720)
                .WithSfxVolume(0.25f)
                .WithMusicVolume(0.4f)
                .WithMouseSensitivity(0.2f);

            Assert.That(d.InvertY, Is.True);
            Assert.That(d.Fullscreen, Is.False);
            Assert.That(d.ResolutionWidth, Is.EqualTo(1280));
            Assert.That(d.ResolutionHeight, Is.EqualTo(720));
            Assert.That(d.SfxVolume, Is.EqualTo(0.25f));
            Assert.That(d.MusicVolume, Is.EqualTo(0.4f));
            Assert.That(d.MouseSensitivity, Is.EqualTo(0.2f));
            Assert.That(d, Is.EqualTo(d));
        }
    }
}
