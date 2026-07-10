using NUnit.Framework;

namespace Doom.Game.Tests
{
    public class LevelStatsTests
    {
        [Test]
        public void Counts_do_not_exceed_totals()
        {
            var s = new LevelStats();
            s.SetTotals(killTotal: 2, itemTotal: 1, secretTotal: 1);

            Assert.That(s.TryRegisterKill(0), Is.True);
            Assert.That(s.TryRegisterKill(1), Is.True);
            Assert.That(s.TryRegisterKill(2), Is.False);
            Assert.That(s.Kills, Is.EqualTo(2));
            Assert.That(s.Snapshot().KillPercent, Is.EqualTo(100));

            Assert.That(s.TryRegisterKill(0), Is.False);
            Assert.That(s.Kills, Is.EqualTo(2));
        }

        [Test]
        public void Duplicate_events_are_ignored()
        {
            var s = new LevelStats();
            s.SetTotals(5, 5, 5);

            Assert.That(s.TryRegisterItem(10), Is.True);
            Assert.That(s.TryRegisterItem(10), Is.False);
            Assert.That(s.Items, Is.EqualTo(1));

            Assert.That(s.TryRegisterSecret(3), Is.True);
            Assert.That(s.TryRegisterSecret(3), Is.False);
            Assert.That(s.Secrets, Is.EqualTo(1));
        }

        [Test]
        public void Time_advances_in_gameplay_tics_only()
        {
            var s = new LevelStats();
            s.AdvanceTics(35);
            s.AdvanceTics(0);
            s.AdvanceTics(-5);
            Assert.That(s.Tics, Is.EqualTo(35));
            s.AdvanceTics(35);
            Assert.That(s.Tics, Is.EqualTo(70));
        }

        [Test]
        public void Snapshot_percent_is_zero_when_total_is_zero()
        {
            var s = new LevelStats();
            s.SetTotals(0, 0, 0);
            var snap = s.Snapshot();
            Assert.That(snap.KillPercent, Is.EqualTo(0));
            Assert.That(snap.ItemPercent, Is.EqualTo(0));
            Assert.That(snap.SecretPercent, Is.EqualTo(0));
        }

        [Test]
        public void Count_item_table_matches_E1_bonuses()
        {
            Assert.That(LevelStats.IsCountItem(2014), Is.True);
            Assert.That(LevelStats.IsCountItem(2013), Is.True);
            Assert.That(LevelStats.IsCountItem(2023), Is.True);
            Assert.That(LevelStats.IsCountItem(2011), Is.False); // stim
            Assert.That(LevelStats.IsCountItem(2001), Is.False); // shotgun
            Assert.That(LevelStats.IsCountItem(2007), Is.False); // clip drop
        }
    }
}
