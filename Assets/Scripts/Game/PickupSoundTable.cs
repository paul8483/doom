namespace Doom.Game
{
    public enum PickupSoundKind
    {
        None,
        Item,
        Weapon,
        Power,
    }

    /// Maps doomednum → pickup SFX class. Does not play audio.
    public static class PickupSoundTable
    {
        public static PickupSoundKind Get(int doomedNum)
        {
            switch (doomedNum)
            {
                case 2001: // shotgun
                case 2002: // chaingun
                case 2003: // rocket launcher
                    return PickupSoundKind.Weapon;

                case 2013: // soulsphere
                case 2023: // berserk
                case 2025: // radiation suit
                    return PickupSoundKind.Power;

                default:
                    return ItemRules.IsPickup(doomedNum)
                        ? PickupSoundKind.Item
                        : PickupSoundKind.None;
            }
        }

        public static string LumpName(PickupSoundKind kind) => kind switch
        {
            PickupSoundKind.Item => "DSITEMUP",
            PickupSoundKind.Weapon => "DSWPNUP",
            PickupSoundKind.Power => "DSGETPOW",
            _ => null,
        };
    }
}
