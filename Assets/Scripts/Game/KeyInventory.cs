namespace Doom.Game
{
    /// Which keys the player holds. Give is idempotent (false if already owned);
    /// ItemRules still accepts key pickups so the GO is destroyed.
    public sealed class KeyInventory
    {
        /// Bits for all six <see cref="PlayerKey"/> values.
        public const int AllKeysMask = (1 << 6) - 1;

        int bits;

        public bool Give(PlayerKey key)
        {
            int mask = 1 << (int)key;
            if ((bits & mask) != 0) return false;
            bits |= mask;
            return true;
        }

        public bool Has(PlayerKey key) => (bits & (1 << (int)key)) != 0;

        public bool HasAny() => bits != 0;

        public void Reset() => bits = 0;

        public int CaptureBits() => bits;

        public void RestoreBits(int keyBits)
        {
            if (keyBits < 0) keyBits = 0;
            bits = keyBits & AllKeysMask;
        }
    }
}
