using Doom.Map;

namespace Doom.Specials
{
    /// P_ChangeSwitchTexture's texture pairing (p_switch.c). Vanilla ships a
    /// fixed switchlist table; Freedoom follows the SW1*/SW2* naming convention
    /// one-to-one, so the pair is derived from the name instead of a table.
    /// The slot to swap follows vanilla's check order: top, then mid, then
    /// bottom — the first slot carrying a switch texture wins.
    public static class SwitchTextureRules
    {
        public enum Slot
        {
            None,
            Upper,
            Middle,
            Lower,
        }

        /// SW1XXX &lt;-&gt; SW2XXX. False for anything else (including bare "SW1").
        public static bool TryGetCounterpart(string texture, out string other)
        {
            other = null;
            if (string.IsNullOrEmpty(texture) || texture.Length <= 3) return false;
            if (texture.StartsWith("SW1", System.StringComparison.OrdinalIgnoreCase))
            {
                other = "SW2" + texture.Substring(3);
                return true;
            }
            if (texture.StartsWith("SW2", System.StringComparison.OrdinalIgnoreCase))
            {
                other = "SW1" + texture.Substring(3);
                return true;
            }
            return false;
        }

        /// First sidedef slot (top -> mid -> bottom) holding a switch texture.
        public static Slot FindSlot(in SideDef side, out string from, out string to)
        {
            if (TryGetCounterpart(side.UpperTexture, out to))
            {
                from = side.UpperTexture;
                return Slot.Upper;
            }
            if (TryGetCounterpart(side.MiddleTexture, out to))
            {
                from = side.MiddleTexture;
                return Slot.Middle;
            }
            if (TryGetCounterpart(side.LowerTexture, out to))
            {
                from = side.LowerTexture;
                return Slot.Lower;
            }
            from = null;
            to = null;
            return Slot.None;
        }

        /// A copy of the sidedef with one texture slot replaced.
        public static SideDef WithSlot(in SideDef side, Slot slot, string texture) =>
            new SideDef(
                side.TextureXOffset, side.TextureYOffset,
                slot == Slot.Upper ? texture : side.UpperTexture,
                slot == Slot.Lower ? texture : side.LowerTexture,
                slot == Slot.Middle ? texture : side.MiddleTexture,
                side.SectorIdx);

        /// Vanilla BUTTONTIME: a repeatable switch pops back after 35 tics.
        public const float ButtonSeconds = 1f;
    }
}
