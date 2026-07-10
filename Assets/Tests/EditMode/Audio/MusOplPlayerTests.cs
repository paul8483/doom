using System;
using System.Collections.Generic;
using System.IO;
using Doom.Wad;
using NUnit.Framework;
using UnityEngine;

namespace Doom.Audio.Tests
{
    public class MusOplPlayerTests
    {
        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void Tick_program_and_note_write_opl_registers()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var chip = new FakeChip();
            var player = new MusOplPlayer(NoteSong(), GenMidiBank.Read(wad.ReadLump("GENMIDI")), chip);

            player.Start(loop: false);
            chip.Writes.Clear();
            player.Tick();

            Assert.That(chip.Writes, Is.Not.Empty);
            Assert.That(chip.Writes.Exists(w => (w.Register & 0xF0) == 0xA0), Is.True);
            Assert.That(chip.Writes.Exists(w => (w.Register & 0xF0) == 0xB0 && (w.Value & 0x20) != 0), Is.True);
        }

        [Test]
        public void Render_44100_frames_covers_one_second_at_140hz()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var chip = new FakeChip();
            var player = new MusOplPlayer(LongDelaySong(), GenMidiBank.Read(wad.ReadLump("GENMIDI")), chip);
            player.Start(loop: true);
            player.Render(new float[44100 * 2], 44100);

            Assert.That(chip.RenderedFrames, Is.EqualTo(44100));
            Assert.That(chip.RenderCalls, Is.EqualTo(140));
        }

        [Test]
        public void Reset_produces_identical_initial_audio()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var chip = new FakeChip();
            var player = new MusOplPlayer(NoteSong(), GenMidiBank.Read(wad.ReadLump("GENMIDI")), chip);
            var first = new float[1024];
            var second = new float[1024];

            player.Start(loop: false);
            player.Render(first, first.Length / 2);
            player.Start(loop: false);
            player.Render(second, second.Length / 2);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Loop_resets_and_continues()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var chip = new FakeChip();
            var player = new MusOplPlayer(NoteSong(), GenMidiBank.Read(wad.ReadLump("GENMIDI")), chip);
            player.Start(loop: true);
            for (int i = 0; i < 20; i++) Assert.That(player.Tick(), Is.True);
            Assert.That(player.IsPlaying, Is.True);
            Assert.That(chip.ResetCount, Is.GreaterThan(1));
        }

        [Test]
        public void Render_does_not_allocate_after_warmup()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var chip = new FakeChip();
            var player = new MusOplPlayer(LongDelaySong(), GenMidiBank.Read(wad.ReadLump("GENMIDI")), chip);
            var output = new float[2048];
            player.Start(loop: true);
            player.Render(output, 1024);
            long before = GC.GetAllocatedBytesForCurrentThread();
            player.Render(output, 1024);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.LessThanOrEqualTo(64));
        }

        [Test]
        public void Freedoom_e1m1_genmidi_smoke_renders_finite_audio()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var player = new MusOplPlayer(
                MusicScore.Read(wad.ReadLump("D_E1M1")),
                GenMidiBank.Read(wad.ReadLump("GENMIDI")),
                new NukedOplChip());
            var output = new float[44100 * 5 * 2];
            player.Start(loop: true);
            player.Render(output, output.Length / 2);

            float peak = 0;
            for (int i = 0; i < output.Length; i++)
            {
                Assert.That(float.IsFinite(output[i]), Is.True);
                peak = Math.Max(peak, Math.Abs(output[i]));
            }
            Assert.That(peak, Is.GreaterThan(0f));
            Assert.That(peak, Is.LessThanOrEqualTo(1f));
        }

        private static MusSong NoteSong() => new MusSong(0, 0, 1, 0, Array.Empty<ushort>(), new[]
        {
            new MusEvent(MusEventType.Controller, 0, 0, 0, 0),
            new MusEvent(MusEventType.Play, 0, 60, 100, 1),
            new MusEvent(MusEventType.ScoreEnd, 0, 0, 0, 0),
        });

        private static MusSong LongDelaySong() => new MusSong(0, 0, 1, 0, Array.Empty<ushort>(), new[]
        {
            new MusEvent(MusEventType.Play, 0, 60, 100, 100000),
            new MusEvent(MusEventType.ScoreEnd, 0, 0, 0, 0),
        });

        private sealed class FakeChip : IOplChip
        {
            public readonly List<(int Register, byte Value)> Writes = new List<(int, byte)>();
            public int RenderedFrames;
            public int ResetCount;
            public int RenderCalls;
            private int _phase;

            public void Reset(int sampleRate)
            {
                ResetCount++;
                _phase = 0;
            }

            public void WriteRegister(int register, byte value)
            {
                Writes.Add((register, value));
            }

            public void Render(float[] stereoInterleaved, int frameOffset, int frameCount)
            {
                RenderedFrames += frameCount;
                RenderCalls++;
                int start = frameOffset * 2;
                for (int i = 0; i < frameCount * 2; i++)
                    stereoInterleaved[start + i] = ((_phase++ & 31) - 16) / 16f;
            }
        }
    }
}
