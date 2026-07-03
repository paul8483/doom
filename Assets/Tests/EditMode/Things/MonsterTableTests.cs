using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Things;
using Doom.Wad;
using Doom.Graphics;

namespace Doom.Things.Tests
{
    public class MonsterTableTests
    {
        static readonly int[] Eds = { 3004, 9, 3001, 3002 };

        [Test]
        public void All_four_monsters_have_consistent_defs()
        {
            foreach (int ed in Eds)
            {
                Assert.That(MonsterTable.TryGet(ed, out var m), Is.True, $"doomednum {ed}");
                Assert.That(m.Speed, Is.GreaterThan(0), $"{ed} speed");
                Assert.That(m.PainChance, Is.InRange(1, 255), $"{ed} painChance");
                Assert.That(m.ReactionMoves, Is.EqualTo(8), $"{ed} reaction");
                foreach (var (seq, name) in new[] {
                    (m.Stand, "stand"), (m.Run, "run"), (m.Attack, "attack"),
                    (m.Pain, "pain"), (m.Death, "death") })
                {
                    Assert.That(seq.Frames.Length, Is.EqualTo(seq.Tics.Length), $"{ed} {name}");
                    Assert.That(seq.Frames.Length, Is.GreaterThan(0), $"{ed} {name} пустая");
                    foreach (int t in seq.Tics) Assert.That(t, Is.GreaterThan(0), $"{ed} {name} tics");
                }
                Assert.That(m.FireIndex, Is.InRange(0, m.Attack.Frames.Length - 1), $"{ed} fireIndex");
                // Хоть какая-то атака есть.
                Assert.That(m.MeleeMod > 0 || m.HitscanCount > 0 || m.HasMissile, $"{ed} атаки");
            }
        }

        [Test]
        public void Doom_data_values()
        {
            MonsterTable.TryGet(3004, out var poss);  // зомби
            Assert.That(poss.Speed, Is.EqualTo(8));
            Assert.That(poss.PainChance, Is.EqualTo(200));
            Assert.That(poss.HitscanCount, Is.EqualTo(1));
            Assert.That(poss.MeleeMod, Is.EqualTo(0));
            Assert.That(poss.HasMissile, Is.False);
            Assert.That(poss.Run.Tics[0], Is.EqualTo(4));       // AABBCCDD @4
            Assert.That(poss.Run.Frames, Is.EqualTo(new[] { 0, 0, 1, 1, 2, 2, 3, 3 }));
            Assert.That(poss.Attack.Frames, Is.EqualTo(new[] { 4, 5, 4 })); // E,F,E
            Assert.That(poss.Attack.Tics, Is.EqualTo(new[] { 10, 8, 8 }));
            Assert.That(poss.FireIndex, Is.EqualTo(1));          // огонь на F
            Assert.That(poss.Death.Frames, Is.EqualTo(new[] { 7, 8, 9, 10 })); // H..K, труп L=11 в ThingTable

            MonsterTable.TryGet(9, out var spos);     // сержант
            Assert.That(spos.PainChance, Is.EqualTo(170));
            Assert.That(spos.HitscanCount, Is.EqualTo(3));
            Assert.That(spos.Run.Tics[0], Is.EqualTo(3));
            Assert.That(spos.Attack.Tics, Is.EqualTo(new[] { 10, 10, 10 }));

            MonsterTable.TryGet(3001, out var troo);  // имп
            Assert.That(troo.PainChance, Is.EqualTo(200));
            Assert.That(troo.MeleeMod, Is.EqualTo(8));
            Assert.That(troo.MeleeMult, Is.EqualTo(3));
            Assert.That(troo.HasMissile, Is.True);
            Assert.That(troo.MissileSpeed, Is.EqualTo(10));      // юниты/тик
            Assert.That(troo.MissileSprite, Is.EqualTo("BAL1"));
            Assert.That(troo.Attack.Frames, Is.EqualTo(new[] { 4, 5, 6 })); // E,F,G
            Assert.That(troo.Attack.Tics, Is.EqualTo(new[] { 8, 8, 6 }));
            Assert.That(troo.FireIndex, Is.EqualTo(2));
            Assert.That(troo.Pain.Tics, Is.EqualTo(new[] { 2, 2 }));
            Assert.That(troo.Death.Frames, Is.EqualTo(new[] { 8, 9, 10, 11 })); // I..L, труп M=12

            MonsterTable.TryGet(3002, out var sarg);  // демон
            Assert.That(sarg.Speed, Is.EqualTo(10));
            Assert.That(sarg.PainChance, Is.EqualTo(180));
            Assert.That(sarg.MeleeMod, Is.EqualTo(10));
            Assert.That(sarg.MeleeMult, Is.EqualTo(4));
            Assert.That(sarg.HitscanCount, Is.EqualTo(0));
            Assert.That(sarg.HasMissile, Is.False);
            Assert.That(sarg.Run.Tics[0], Is.EqualTo(2));
            Assert.That(sarg.Death.Frames, Is.EqualTo(new[] { 8, 9, 10, 11, 12 })); // I..M, труп N=13
        }

        [Test]
        public void All_sequence_frames_resolve_in_freedoom()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var sprites = SpriteSet.Load(wad);
            foreach (int ed in Eds)
            {
                MonsterTable.TryGet(ed, out var m);
                ThingTable.TryGet(ed, out var thing);
                foreach (var seq in new[] { m.Stand, m.Run, m.Attack, m.Pain, m.Death })
                    foreach (int f in seq.Frames)
                        Assert.That(sprites.TryGet(thing.Sprite, f, 0, out _), Is.True,
                            $"{thing.Sprite} кадр {f} (ed {ed})");
            }
            // Фаербол импа: полёт A,B + взрыв C,D,E.
            MonsterTable.TryGet(3001, out var imp);
            foreach (int f in imp.MissileFlyFrames) Assert.That(sprites.TryGet("BAL1", f, 0, out _), Is.True, $"BAL1 fly {f}");
            foreach (int f in imp.MissileExplodeFrames) Assert.That(sprites.TryGet("BAL1", f, 0, out _), Is.True, $"BAL1 boom {f}");
        }
    }
}
