using System;

namespace Doom.Game
{
    /// Thrown when a save blob fails envelope, checksum, bounds, or schema checks.
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message) { }

        public SaveFormatException(string message, Exception inner) : base(message, inner) { }
    }
}
