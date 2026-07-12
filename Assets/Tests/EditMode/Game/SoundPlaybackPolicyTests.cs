using NUnit.Framework;
using Doom.Game;
using Doom.MapBuild;

namespace Doom.Game.Tests
{
    public class SoundPlaybackPolicyTests
    {
        [Test]
        public void Selects_idle_channel_before_stealing()
        {
            var channels = new[]
            {
                new SoundChannelState(true, false, SoundPriority.Ambient, 1),
                new SoundChannelState(false, false, SoundPriority.Critical, 2),
            };

            Assert.That(SoundPlaybackPolicy.SelectChannel(channels, SoundPriority.World), Is.EqualTo(1));
        }

        [Test]
        public void Steals_lowest_priority_then_oldest()
        {
            var channels = new[]
            {
                new SoundChannelState(true, false, SoundPriority.World, 30),
                new SoundChannelState(true, false, SoundPriority.Ambient, 20),
                new SoundChannelState(true, false, SoundPriority.Ambient, 10),
            };

            Assert.That(SoundPlaybackPolicy.SelectChannel(channels, SoundPriority.Player), Is.EqualTo(2));
        }

        [Test]
        public void Protects_loops_and_higher_priority_channels()
        {
            var channels = new[]
            {
                new SoundChannelState(false, true, SoundPriority.Ambient, 1),
                new SoundChannelState(true, false, SoundPriority.Player, 2),
            };

            Assert.That(SoundPlaybackPolicy.SelectChannel(channels, SoundPriority.World), Is.EqualTo(-1));
        }

        [Test]
        public void Metadata_keeps_local_cues_stable_and_varies_suitable_world_cues()
        {
            Assert.That(SoundPlaybackPolicy.Describe("DSPISTOL", local: true).PitchVariation,
                Is.EqualTo(SoundPitchVariation.None));
            Assert.That(SoundPlaybackPolicy.Describe("DSPOSIT1", local: false).Priority,
                Is.EqualTo(SoundPriority.Monster));
            Assert.That(SoundPlaybackPolicy.Describe(
                    "DSPISTOL", local: false, context: SoundCueContext.Monster).Priority,
                Is.EqualTo(SoundPriority.Monster));
            Assert.That(SoundPlaybackPolicy.Describe("DSDOROPN", local: false).PitchVariation,
                Is.EqualTo(SoundPitchVariation.DoomWide));
            Assert.That(SoundPlaybackPolicy.Describe("DSSTNMOV", local: false, loop: true).PitchVariation,
                Is.EqualTo(SoundPitchVariation.None));
        }

        [Test]
        public void Pitch_variation_uses_deterministic_doom_random_values()
        {
            var metadata = SoundPlaybackPolicy.Describe("DSPOSIT1", local: false);
            var random = new DoomRandom();

            Assert.That(SoundPlaybackPolicy.ResolvePitch(metadata, random), Is.EqualTo(1.0625f));
            Assert.That(SoundPlaybackPolicy.ResolvePitch(metadata, random), Is.EqualTo(1.0234375f));
        }

        [Test]
        public void Map_loader_prewarm_includes_teleport()
        {
            CollectionAssert.Contains(MapLoader.CollectSfxNames(), "DSTELEPT");
        }
    }
}
