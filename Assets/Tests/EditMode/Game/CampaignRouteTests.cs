using System;
using System.Collections.Generic;
using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class CampaignRouteTests
    {
        static readonly string[] FullE1 =
        {
            "E1M1", "E1M2", "E1M3", "E1M4", "E1M5", "E1M6", "E1M7", "E1M8", "E1M9"
        };

        [TestCase("E1M1", "E1M2")]
        [TestCase("E1M2", "E1M3")]
        [TestCase("E1M3", "E1M4")]
        [TestCase("E1M4", "E1M5")]
        [TestCase("E1M5", "E1M6")]
        [TestCase("E1M6", "E1M7")]
        [TestCase("E1M7", "E1M8")]
        [TestCase("E1M9", "E1M4")]
        public void Normal_exit_follows_route_table(string current, string expectedNext)
        {
            var r = CampaignRoute.Resolve(current, ExitKind.Normal, FullE1);
            Assert.That(r.Outcome, Is.EqualTo(CampaignOutcome.NextMap));
            Assert.That(r.NextMap, Is.EqualTo(expectedNext));
            Assert.That(r.UsedSecretFallback, Is.False);
        }

        [Test]
        public void Normal_exit_from_E1M8_completes_episode()
        {
            var r = CampaignRoute.Resolve("E1M8", ExitKind.Normal, FullE1);
            Assert.That(r.Outcome, Is.EqualTo(CampaignOutcome.EpisodeComplete));
            Assert.That(r.NextMap, Is.Null);
        }

        [TestCase("E1M1")]
        [TestCase("E1M2")]
        [TestCase("E1M3")]
        [TestCase("E1M4")]
        [TestCase("E1M5")]
        [TestCase("E1M6")]
        [TestCase("E1M7")]
        [TestCase("E1M8")]
        public void Secret_exit_goes_to_E1M9(string current)
        {
            var r = CampaignRoute.Resolve(current, ExitKind.Secret, FullE1);
            Assert.That(r.Outcome, Is.EqualTo(CampaignOutcome.NextMap));
            Assert.That(r.NextMap, Is.EqualTo("E1M9"));
            Assert.That(r.UsedSecretFallback, Is.False);
        }

        [Test]
        public void Secret_exit_from_E1M9_returns_to_E1M4()
        {
            var r = CampaignRoute.Resolve("E1M9", ExitKind.Secret, FullE1);
            Assert.That(r.NextMap, Is.EqualTo("E1M4"));
        }

        [Test]
        public void Normalize_is_case_insensitive()
        {
            Assert.That(CampaignRoute.TryNormalize("e1m1", out string c), Is.True);
            Assert.That(c, Is.EqualTo("E1M1"));

            Assert.That(CampaignMap.TryParse("e1m3", out var map), Is.True);
            Assert.That(map.Name, Is.EqualTo("E1M3"));
        }

        [TestCase("MAP01")]
        [TestCase("E1M10")]
        [TestCase("E0M1")]
        [TestCase("E5M1")]
        [TestCase("E1MX")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("M1")]
        public void Rejects_malformed_or_doom2_names(string raw)
        {
            Assert.That(CampaignRoute.TryNormalize(raw, out _), Is.False);
            Assert.That(CampaignMap.TryParse(raw, out _), Is.False);
            Assert.Throws<ArgumentException>(
                () => CampaignRoute.Resolve(raw, ExitKind.Normal, FullE1));
        }

        [Test]
        public void Rejects_map_outside_E1_route_table()
        {
            Assert.Throws<ArgumentException>(
                () => CampaignRoute.Resolve("E2M1", ExitKind.Normal, new[] { "E2M1", "E2M2" }));
        }

        [Test]
        public void Missing_normal_target_throws()
        {
            var withoutM2 = new[] { "E1M1", "E1M3", "E1M4", "E1M5", "E1M6", "E1M7", "E1M8", "E1M9" };
            Assert.Throws<InvalidOperationException>(
                () => CampaignRoute.Resolve("E1M1", ExitKind.Normal, withoutM2));
        }

        [Test]
        public void Missing_secret_target_falls_back_to_normal()
        {
            var withoutM9 = new[] { "E1M1", "E1M2", "E1M3", "E1M4", "E1M5", "E1M6", "E1M7", "E1M8" };
            var r = CampaignRoute.Resolve("E1M3", ExitKind.Secret, withoutM9);
            Assert.That(r.Outcome, Is.EqualTo(CampaignOutcome.NextMap));
            Assert.That(r.NextMap, Is.EqualTo("E1M4"));
            Assert.That(r.UsedSecretFallback, Is.True);
        }

        [Test]
        public void Missing_secret_from_E1M8_falls_back_to_episode_complete()
        {
            var withoutM9 = new[] { "E1M1", "E1M2", "E1M3", "E1M4", "E1M5", "E1M6", "E1M7", "E1M8" };
            var r = CampaignRoute.Resolve("E1M8", ExitKind.Secret, withoutM9);
            Assert.That(r.Outcome, Is.EqualTo(CampaignOutcome.EpisodeComplete));
            Assert.That(r.UsedSecretFallback, Is.True);
        }

        [Test]
        public void Available_maps_accepted_case_insensitively()
        {
            var mixed = new List<string> { "e1m1", "E1m2", "E1M3", "e1M4", "E1M5", "E1M6", "E1M7", "E1M8", "e1m9" };
            var r = CampaignRoute.Resolve("e1m1", ExitKind.Normal, mixed);
            Assert.That(r.NextMap, Is.EqualTo("E1M2"));
        }
    }
}
