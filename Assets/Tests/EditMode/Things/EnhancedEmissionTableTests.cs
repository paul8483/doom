using NUnit.Framework;
using Doom.Things;

namespace Doom.Things.Tests
{
    public class EnhancedEmissionTableTests
    {
        [Test]
        public void Known_E1_lamps_and_firesticks_resolve()
        {
            int[] known =
            {
                2028, 35, 34, 29,
                44, 45, 46, 55, 56, 57, 70,
            };
            foreach (int n in known)
            {
                Assert.IsTrue(EnhancedEmissionTable.TryGet(n, out var def),
                    $"expected emission for doomednum {n}");
                Assert.Greater(def.Intensity, 0f);
                Assert.Greater(def.RangeDoom, 0f);
                Assert.Greater(def.Importance, 0f);
            }
        }

        [Test]
        public void Unknown_thing_has_no_unity_light_entry()
        {
            Assert.IsFalse(EnhancedEmissionTable.TryGet(3004, out _)); // POSS
            Assert.IsFalse(EnhancedEmissionTable.TryGet(2035, out _)); // barrel (explosion is event)
            Assert.IsFalse(EnhancedEmissionTable.Contains(1));
        }

        [Test]
        public void Firesticks_request_warm_colored_light()
        {
            Assert.IsTrue(EnhancedEmissionTable.TryGet(46, out var red));
            Assert.Greater(red.ColorR, red.ColorG);
            Assert.IsTrue(red.WantsShadow);

            Assert.IsTrue(EnhancedEmissionTable.TryGet(44, out var blue));
            Assert.Greater(blue.ColorB, blue.ColorR);
        }
    }
}
