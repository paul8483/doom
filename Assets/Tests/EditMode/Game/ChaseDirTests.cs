using System.Collections.Generic;
using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class ChaseDirTests
    {
        static ChaseDir.TryStepFn Allow(params Dir8[] allowed)
        {
            var set = new HashSet<Dir8>(allowed);
            return d => set.Contains(d);
        }

        [Test]
        public void Prefers_diagonal_when_both_axes_far()
        {
            var r = new DoomRandom();
            var dir = ChaseDir.NewChaseDir(dx: 100f, dy: 100f, current: Dir8.None,
                r, d => true, out _);
            Assert.That(dir, Is.EqualTo(Dir8.NorthEast));
        }

        [Test]
        public void Falls_back_to_axis_when_diagonal_blocked()
        {
            var r = new DoomRandom();
            var dir = ChaseDir.NewChaseDir(100f, 100f, Dir8.None,
                r, Allow(Dir8.East, Dir8.North), out _);
            // Unity's NUnit (3.5) has no Is.AnyOf — use an Or-constraint instead.
            Assert.That(dir, Is.EqualTo(Dir8.East).Or.EqualTo(Dir8.North));
        }

        [Test]
        public void Never_picks_turnaround_unless_cornered()
        {
            var r = new DoomRandom();
            // Идём на восток; всё, кроме разворота, заблокировано → берёт разворот.
            var dir = ChaseDir.NewChaseDir(100f, 0f, Dir8.East, r, Allow(Dir8.West), out _);
            Assert.That(dir, Is.EqualTo(Dir8.West));
            // А если открыт хоть один другой путь — разворот не выбирается.
            for (int seed = 0; seed < 8; seed++)
            {
                dir = ChaseDir.NewChaseDir(100f, 0f, Dir8.East,
                    new DoomRandom(seed), Allow(Dir8.West, Dir8.North), out _);
                Assert.That(dir, Is.EqualTo(Dir8.North), $"seed {seed}");
            }
        }

        [Test]
        public void Fully_blocked_returns_none()
        {
            var r = new DoomRandom();
            var dir = ChaseDir.NewChaseDir(100f, 0f, Dir8.East, r, d => false, out int mc);
            Assert.That(dir, Is.EqualTo(Dir8.None));
        }

        [Test]
        public void Movecount_is_random_and_15_masked()
        {
            for (int seed = 0; seed < 32; seed++)
            {
                ChaseDir.NewChaseDir(100f, 100f, Dir8.None, new DoomRandom(seed),
                    d => true, out int mc);
                Assert.That(mc, Is.InRange(0, 15));
            }
        }

        [Test]
        public void Small_deltas_mean_no_axis_preference()
        {
            // |delta| <= 10 юнитов по оси — ось не считается направлением (порог из P_NewChaseDir).
            var r = new DoomRandom();
            var dir = ChaseDir.NewChaseDir(5f, 100f, Dir8.None, r, d => true, out _);
            Assert.That(dir, Is.EqualTo(Dir8.North));
        }
    }
}
