namespace Doom.Game
{
    /// Player-side key bits. Distinct from Doom.Specials.KeyKind so Doom.Game
    /// stays free of a Specials reference; MapBuild maps between them.
    public enum PlayerKey
    {
        BlueCard,
        YellowCard,
        RedCard,
        BlueSkull,
        YellowSkull,
        RedSkull,
    }
}
