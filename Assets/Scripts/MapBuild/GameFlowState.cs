namespace Doom.MapBuild
{
    /// Mutually exclusive high-level UI/gameplay states. Owned by
    /// <see cref="GameFlowController"/> — death, intermission, and pause never overlap.
    public enum GameFlowState
    {
        Boot,
        MainMenu,
        Loading,
        Playing,
        Paused,
        Dead,
        Intermission,
    }
}
