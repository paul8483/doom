using Doom.Game;
using Doom.Specials;

namespace Doom.MapBuild
{
    /// Maps Doom.Specials.KeyKind ↔ Doom.Game.PlayerKey (keeps Game free of Specials).
    public static class KeyMapping
    {
        public static bool HasRequired(KeyInventory keys, KeyKind required)
        {
            if (required == KeyKind.None) return true;
            if (required == KeyKind.Any) return keys.HasAny();
            return ToPlayerKey(required, out var pk) && keys.Has(pk);
        }

        public static bool ToPlayerKey(KeyKind kind, out PlayerKey key)
        {
            switch (kind)
            {
                case KeyKind.BlueCard:    key = PlayerKey.BlueCard; return true;
                case KeyKind.YellowCard:  key = PlayerKey.YellowCard; return true;
                case KeyKind.RedCard:     key = PlayerKey.RedCard; return true;
                case KeyKind.BlueSkull:   key = PlayerKey.BlueSkull; return true;
                case KeyKind.YellowSkull: key = PlayerKey.YellowSkull; return true;
                case KeyKind.RedSkull:    key = PlayerKey.RedSkull; return true;
                default: key = default; return false;
            }
        }
    }
}
