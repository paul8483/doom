using NUnit.Framework;
using Doom.Map;
using Doom.Specials;

namespace Doom.Specials.Tests
{
    public class SwitchTextureRulesTests
    {
        [Test]
        public void Counterpart_maps_sw1_and_sw2_both_ways()
        {
            Assert.IsTrue(SwitchTextureRules.TryGetCounterpart("SW1COMP", out var a));
            Assert.AreEqual("SW2COMP", a);
            Assert.IsTrue(SwitchTextureRules.TryGetCounterpart("SW2PIPE", out var b));
            Assert.AreEqual("SW1PIPE", b);
        }

        [Test]
        public void Counterpart_rejects_non_switch_names()
        {
            Assert.IsFalse(SwitchTextureRules.TryGetCounterpart("STARTAN2", out _));
            Assert.IsFalse(SwitchTextureRules.TryGetCounterpart("SW1", out _));
            Assert.IsFalse(SwitchTextureRules.TryGetCounterpart("", out _));
            Assert.IsFalse(SwitchTextureRules.TryGetCounterpart(null, out _));
        }

        [Test]
        public void Slot_priority_is_top_then_mid_then_bottom()
        {
            // Vanilla P_ChangeSwitchTexture checks toptexture first.
            var side = new SideDef(0, 0, "SW1COMP", "SW1BRN1", "SW1EXIT", 0);
            var slot = SwitchTextureRules.FindSlot(side, out var from, out var to);
            Assert.AreEqual(SwitchTextureRules.Slot.Upper, slot);
            Assert.AreEqual("SW1COMP", from);
            Assert.AreEqual("SW2COMP", to);

            var midOnly = new SideDef(0, 0, "STARTAN2", "-", "SW1EXIT", 0);
            slot = SwitchTextureRules.FindSlot(midOnly, out from, out to);
            Assert.AreEqual(SwitchTextureRules.Slot.Middle, slot);
            Assert.AreEqual("SW2EXIT", to);

            var none = new SideDef(0, 0, "STARTAN2", "-", "-", 0);
            Assert.AreEqual(SwitchTextureRules.Slot.None,
                SwitchTextureRules.FindSlot(none, out _, out _));
        }

        [Test]
        public void WithSlot_replaces_only_the_named_slot()
        {
            var side = new SideDef(8, 16, "SW1COMP", "LOW", "MID", 7);
            var swapped = SwitchTextureRules.WithSlot(
                side, SwitchTextureRules.Slot.Upper, "SW2COMP");
            Assert.AreEqual("SW2COMP", swapped.UpperTexture);
            Assert.AreEqual("LOW", swapped.LowerTexture);
            Assert.AreEqual("MID", swapped.MiddleTexture);
            Assert.AreEqual(8, swapped.TextureXOffset);
            Assert.AreEqual(16, swapped.TextureYOffset);
            Assert.AreEqual(7, swapped.SectorIdx);
        }

        [Test]
        public void Every_e1_switch_pair_exists_in_the_wad()
        {
            // The pairing is name-derived, so the WAD must actually carry the
            // SW2 counterpart of every SW1 used on E1 (and vice versa).
            string path = System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!System.IO.File.Exists(path)) Assert.Ignore("freedoom1.wad missing");
            using var wad = Doom.Wad.WadFile.Open(path);
            var textures = Doom.Graphics.TextureSet.Load(wad);

            foreach (string mapName in new[]
                     { "E1M1", "E1M2", "E1M3", "E1M4", "E1M5", "E1M6", "E1M7", "E1M8", "E1M9" })
            {
                var map = MapData.Load(wad, mapName);
                foreach (var side in map.SideDefs)
                    foreach (var tex in new[]
                             { side.UpperTexture, side.MiddleTexture, side.LowerTexture })
                        if (SwitchTextureRules.TryGetCounterpart(tex, out var other))
                            Assert.IsTrue(textures.Contains(other),
                                $"{mapName}: {tex} has no counterpart {other} in TEXTURE1/2");
            }
        }
    }
}
