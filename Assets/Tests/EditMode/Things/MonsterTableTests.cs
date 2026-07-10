using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;
using Doom.Graphics;

namespace Doom.Things.Tests
{
    public class MonsterTableTests
    {
        static readonly int[] Eds = { 3004, 9, 3001, 3002, 58, 3003 };

        [Test]
        public void All_e1_roster_monsters_have_consistent_defs()
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
                Assert.That(m.Sounds, Is.Not.Null, $"{ed} Sounds");
                Assert.That(m.Sounds.Sight, Is.Not.Null.And.Not.Empty, $"{ed} Sight");
                Assert.That(m.Sounds.Pain, Is.Not.Null.And.Not.Empty, $"{ed} Pain");
                Assert.That(m.Sounds.Death, Is.Not.Null.And.Not.Empty, $"{ed} Death");
                foreach (string s in m.Sounds.Sight.Concat(m.Sounds.Death)
                             .Append(m.Sounds.Pain).Append(m.Sounds.Active)
                             .Append(m.Sounds.RangedAttack).Append(m.Sounds.MeleeAttack))
                {
                    if (string.IsNullOrEmpty(s)) continue;
                    Assert.That(s, Does.StartWith("DS"), $"{ed} sound {s}");
                }
                if (m.HasMissile)
                {
                    Assert.That(m.MissileSprite, Is.Not.Null, $"{ed} missileSprite");
                    foreach (var (frames, tics, name) in new[] {
                        (m.MissileFlyFrames, m.MissileFlyTics, "missile fly"),
                        (m.MissileExplodeFrames, m.MissileExplodeTics, "missile explode") })
                    {
                        Assert.That(frames.Length, Is.EqualTo(tics.Length), $"{ed} {name}");
                        Assert.That(frames.Length, Is.GreaterThan(0), $"{ed} {name} пустая");
                        foreach (int t in tics) Assert.That(t, Is.GreaterThan(0), $"{ed} {name} tics");
                    }
                }
            }
        }

        [Test]
        public void Sound_lumps_decode_in_freedoom()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            foreach (int ed in Eds)
            {
                Assert.That(MonsterTable.TryGet(ed, out var m), Is.True);
                foreach (string name in m.Sounds.Sight.Concat(m.Sounds.Death)
                             .Append(m.Sounds.Pain).Append(m.Sounds.Active)
                             .Append(m.Sounds.RangedAttack).Append(m.Sounds.MeleeAttack))
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    Assert.That(Doom.Audio.SoundCatalog.TryRead(wad, name, out var snd), Is.True, name);
                    Assert.That(snd.Samples, Is.Not.Empty, name);
                }
            }
        }

        [Test]
        public void Doom_data_values()
        {
            Assert.That(MonsterTable.TryGet(3004, out var poss), Is.True, "doomednum 3004");  // зомби
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

            Assert.That(MonsterTable.TryGet(9, out var spos), Is.True, "doomednum 9");     // сержант
            Assert.That(spos.PainChance, Is.EqualTo(170));
            Assert.That(spos.HitscanCount, Is.EqualTo(3));
            Assert.That(spos.Run.Tics[0], Is.EqualTo(3));
            Assert.That(spos.Attack.Tics, Is.EqualTo(new[] { 10, 10, 10 }));

            Assert.That(MonsterTable.TryGet(3001, out var troo), Is.True, "doomednum 3001");  // имп
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

            Assert.That(MonsterTable.TryGet(3002, out var sarg), Is.True, "doomednum 3002");  // демон
            Assert.That(sarg.Speed, Is.EqualTo(10));
            Assert.That(sarg.PainChance, Is.EqualTo(180));
            Assert.That(sarg.MeleeMod, Is.EqualTo(10));
            Assert.That(sarg.MeleeMult, Is.EqualTo(4));
            Assert.That(sarg.HitscanCount, Is.EqualTo(0));
            Assert.That(sarg.HasMissile, Is.False);
            Assert.That(sarg.Run.Tics[0], Is.EqualTo(2));
            Assert.That(sarg.Death.Frames, Is.EqualTo(new[] { 8, 9, 10, 11, 12 })); // I..M, труп N=13

            Assert.That(MonsterTable.TryGet(58, out var spectre), Is.True, "doomednum 58");
            Assert.That(spectre.Speed, Is.EqualTo(sarg.Speed));
            Assert.That(spectre.MeleeMult, Is.EqualTo(sarg.MeleeMult));

            Assert.That(MonsterTable.TryGet(3003, out var boss), Is.True, "doomednum 3003");
            Assert.That(boss.HasMissile, Is.True);
            Assert.That(boss.MissileSprite, Is.EqualTo("BAL7"));
            Assert.That(boss.MissileSpeed, Is.EqualTo(15));
            Assert.That(boss.PainChance, Is.EqualTo(50));
            Assert.That(boss.MeleeMult, Is.EqualTo(10));
            Assert.That(boss.Death.Frames.Length, Is.EqualTo(7));
        }

        [Test]
        public void All_sequence_frames_resolve_in_freedoom()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var sprites = SpriteSet.Load(wad);
            foreach (int ed in Eds)
            {
                Assert.That(MonsterTable.TryGet(ed, out var m), Is.True, $"MonsterTable ed {ed}");
                Assert.That(ThingTable.TryGet(ed, out var thing), Is.True, $"ThingTable ed {ed}");
                foreach (var seq in new[] { m.Stand, m.Run, m.Attack, m.Pain, m.Death })
                    foreach (int f in seq.Frames)
                        Assert.That(sprites.TryGet(thing.Sprite, f, 0, out _), Is.True,
                            $"{thing.Sprite} кадр {f} (ed {ed})");
            }
            // Фаербол импа: полёт A,B + взрыв C,D,E.
            Assert.That(MonsterTable.TryGet(3001, out var imp), Is.True, "MonsterTable ed 3001");
            foreach (int f in imp.MissileFlyFrames) Assert.That(sprites.TryGet("BAL1", f, 0, out _), Is.True, $"BAL1 fly {f}");
            foreach (int f in imp.MissileExplodeFrames) Assert.That(sprites.TryGet("BAL1", f, 0, out _), Is.True, $"BAL1 boom {f}");
            // Baron green ball BAL7.
            Assert.That(MonsterTable.TryGet(3003, out var baron), Is.True);
            foreach (int f in baron.MissileFlyFrames) Assert.That(sprites.TryGet("BAL7", f, 0, out _), Is.True, $"BAL7 fly {f}");
            foreach (int f in baron.MissileExplodeFrames) Assert.That(sprites.TryGet("BAL7", f, 0, out _), Is.True, $"BAL7 boom {f}");
        }
    }
}
