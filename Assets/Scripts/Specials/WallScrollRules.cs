namespace Doom.Specials
{
    /// Pure rules for classic constant wall scrollers. Values are texture pixels
    /// advanced per 35 Hz DOOM tic; the Unity adapter converts them to normalized U.
    public static class WallScrollRules
    {
        public const float TicsPerSecond = 35f;

        public static bool TryGetUnitsPerTic(int linedefSpecial, out float unitsPerTic)
        {
            switch (linedefSpecial)
            {
                case 48: // classic scroll left
                    unitsPerTic = 1f;
                    return true;
                case 85: // Boom-compatible opposite direction
                    unitsPerTic = -1f;
                    return true;
                default:
                    unitsPerTic = 0f;
                    return false;
            }
        }

        public static float NormalizedSpeed(int linedefSpecial, int textureWidth)
        {
            if (textureWidth <= 0 ||
                !TryGetUnitsPerTic(linedefSpecial, out float unitsPerTic))
                return 0f;
            return unitsPerTic * TicsPerSecond / textureWidth;
        }

        public static float NormalizedOffset(int linedefSpecial, int textureWidth, int gameTic)
        {
            if (textureWidth <= 0 ||
                !TryGetUnitsPerTic(linedefSpecial, out float unitsPerTic))
                return 0f;
            double raw = (double)unitsPerTic * gameTic / textureWidth;
            return (float)(raw - System.Math.Floor(raw));
        }
    }
}
