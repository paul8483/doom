using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class DoomRandomTests
    {
        [Test]
        public void First_values_match_doom_rndtable()
        {
            var r = new DoomRandom();
            // rndtable[1..8] — P_Random инкрементирует индекс ПЕРЕД чтением.
            Assert.That(r.Next(), Is.EqualTo(8));
            Assert.That(r.Next(), Is.EqualTo(109));
            Assert.That(r.Next(), Is.EqualTo(220));
            Assert.That(r.Next(), Is.EqualTo(222));
            Assert.That(r.Next(), Is.EqualTo(241));
            Assert.That(r.Next(), Is.EqualTo(149));
            Assert.That(r.Next(), Is.EqualTo(107));
            Assert.That(r.Next(), Is.EqualTo(75));
        }

        [Test]
        public void Wraps_after_256_and_seed_offsets_start()
        {
            var r = new DoomRandom();
            for (int i = 0; i < 256; i++) r.Next();
            Assert.That(r.Next(), Is.EqualTo(8), "после 256 значений индекс заворачивается");

            var seeded = new DoomRandom(seed: 1); // индекс 1 → первое значение rndtable[2]
            Assert.That(seeded.Next(), Is.EqualTo(109));
        }

        [Test]
        public void Values_are_bytes()
        {
            var r = new DoomRandom();
            for (int i = 0; i < 512; i++)
            {
                int v = r.Next();
                Assert.That(v, Is.InRange(0, 255));
            }
        }
    }
}
