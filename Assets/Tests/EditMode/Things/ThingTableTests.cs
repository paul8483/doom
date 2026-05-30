using NUnit.Framework;

namespace Doom.Things.Tests
{
    public class ThingTableTests
    {
        [Test]
        public void Resolves_known_monster()
        {
            Assert.That(ThingTable.TryGet(3001, out var imp), Is.True); // imp
            Assert.That(imp.Sprite, Is.EqualTo("TROO"));
            Assert.That(imp.Has(ThingFlags.Solid), Is.True);
            Assert.That(imp.Has(ThingFlags.Shootable), Is.True);
            Assert.That(imp.Has(ThingFlags.CountKill), Is.True);
        }

        [Test]
        public void Resolves_known_items_and_obstacles()
        {
            Assert.That(ThingTable.TryGet(2014, out var bonus), Is.True); // health bonus
            Assert.That(bonus.Sprite, Is.EqualTo("BON1"));
            Assert.That(bonus.Has(ThingFlags.Solid), Is.False);

            Assert.That(ThingTable.TryGet(2035, out var barrel), Is.True); // barrel
            Assert.That(barrel.Sprite, Is.EqualTo("BAR1"));
            Assert.That(barrel.Has(ThingFlags.Solid), Is.True);
        }

        [Test]
        public void Hanging_decoration_has_spawnceiling()
        {
            Assert.That(ThingTable.TryGet(49, out var gor), Is.True); // hanging victim
            Assert.That(gor.Has(ThingFlags.SpawnCeiling), Is.True);
            Assert.That(gor.Has(ThingFlags.Solid), Is.True); // 49 is hanging + solid

            Assert.That(ThingTable.TryGet(59, out var gorNonSolid), Is.True);
            Assert.That(gorNonSolid.Has(ThingFlags.SpawnCeiling), Is.True);
            Assert.That(gorNonSolid.Has(ThingFlags.Solid), Is.False);
        }

        [Test]
        public void Unknown_type_is_absent()
        {
            Assert.That(ThingTable.TryGet(99999, out _), Is.False);
        }

        [Test]
        public void Player_and_dm_starts_are_absent_from_the_table()
        {
            // Spawn points are filtered by the spawner, not stored as renderable defs.
            Assert.That(ThingTable.TryGet(1, out _), Is.False);
            Assert.That(ThingTable.TryGet(2, out _), Is.False);
            Assert.That(ThingTable.TryGet(11, out _), Is.False);
        }
    }
}
