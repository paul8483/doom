using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Graphics.Tests
{
    public class UiPatchCatalogTests
    {
        static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void Freedoom_loads_required_status_bar_patches()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));
            var catalog = UiPatchCatalog.Load(wad, pal, UiPatchCatalog.StatusBarNames);

            Assert.That(catalog.TryGet("STBAR", out var bar), Is.True);
            Assert.That(bar.Width, Is.GreaterThan(0));
            Assert.That(bar.Height, Is.GreaterThan(0));
            Assert.That(bar.Image.Rgba.Length, Is.EqualTo(bar.Width * bar.Height * 4));

            Assert.That(catalog.TryGet("STTNUM0", out var zero), Is.True);
            Assert.That(zero.Width, Is.GreaterThan(0));
            Assert.That(catalog.TryGet("STYSNUM5", out _), Is.True);
            Assert.That(catalog.TryGet("STGNUM2", out _), Is.True);
            Assert.That(catalog.TryGet("STKEYS0", out _), Is.True);
            Assert.That(catalog.TryGet("STFST00", out var face), Is.True);
            Assert.That(face.Width, Is.GreaterThan(0));
            Assert.That(catalog.TryGet("STFDEAD0", out _), Is.True);
            Assert.That(catalog.TryGet("STARMS", out _), Is.True);
            Assert.That(catalog.TryGet("STTPRCNT", out _), Is.True);
        }

        [Test]
        public void Lookup_is_case_insensitive()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));
            var catalog = UiPatchCatalog.Load(wad, pal, new[] { "STBAR" });

            Assert.That(catalog.TryGet("stbar", out var a), Is.True);
            Assert.That(catalog.TryGet("StBar", out var b), Is.True);
            Assert.That(a.Name, Is.EqualTo("STBAR"));
            Assert.That(b.Width, Is.EqualTo(a.Width));
        }

        [Test]
        public void Header_offsets_match_Patch_ReadHeader()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));
            var catalog = UiPatchCatalog.Load(wad, pal, new[] { "STFST00", "STTNUM7" });

            Assert.That(catalog.TryGet("STFST00", out var face), Is.True);
            var raw = Patch.ReadHeader(wad.ReadLump("STFST00"));
            Assert.That((face.Width, face.Height, face.LeftOffset, face.TopOffset),
                Is.EqualTo((raw.Width, raw.Height, raw.LeftOffset, raw.TopOffset)));
        }

        [Test]
        public void Optional_miss_is_recorded_without_throwing()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));
            var catalog = UiPatchCatalog.LoadStandard(wad, pal);

            Assert.That(catalog.IsMiss("ZZNOUIXX"), Is.True);
            Assert.That(catalog.TryGet("ZZNOUIXX", out _), Is.False);
            Assert.That(catalog.ContainsKey("ZZNOUIXX"), Is.True);

            // Present optional lumps still resolve.
            Assert.That(catalog.TryGet("TITLEPIC", out var title), Is.True);
            Assert.That(title.Width, Is.GreaterThan(0));
            Assert.That(catalog.TryGet("WIMAP0", out _), Is.True);
            Assert.That(catalog.TryGet("M_DOOM", out _), Is.True);
            Assert.That(catalog.TryGet("M_OPTTTL", out _), Is.True);
            Assert.That(catalog.TryGet("M_SFXVOL", out _), Is.True);
            Assert.That(catalog.TryGet("STCFN065", out _), Is.True); // 'A'
        }

        [Test]
        public void Unknown_name_not_in_load_set_is_not_a_miss()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));
            var catalog = UiPatchCatalog.Load(wad, pal, new[] { "STBAR" });

            Assert.That(catalog.TryGet("STTNUM0", out _), Is.False);
            Assert.That(catalog.IsMiss("STTNUM0"), Is.False);
            Assert.That(catalog.ContainsKey("STTNUM0"), Is.False);
        }
    }
}
