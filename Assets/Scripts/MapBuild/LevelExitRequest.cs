using Doom.Game;

namespace Doom.MapBuild
{
    /// One accepted request to leave the current map.
    public readonly struct LevelExitRequest
    {
        public ExitKind Kind { get; }
        /// Source linedef index, or -1 for sector-special exits.
        public int SourceLineIndex { get; }

        public LevelExitRequest(ExitKind kind, int sourceLineIndex)
        {
            Kind = kind;
            SourceLineIndex = sourceLineIndex;
        }
    }
}
