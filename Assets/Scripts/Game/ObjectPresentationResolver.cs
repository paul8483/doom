namespace Doom.Game
{
    /// Per-lump presentation for Classic / Enhanced ± 3D Objects toggle.
    public enum ObjectPresentation
    {
        NativeBillboard = 0,
        RedrawBillboard = 2,
        Mesh = 3,
    }

    /// Pure cascade from the Enhanced 3D Objects Toggle design matrix.
    /// EdgeMix 8× removed 2026-08-08: lumps without AI assets stay native.
    public static class ObjectPresentationResolver
    {
        public static ObjectPresentation Resolve(
            GraphicsMode mode,
            bool toggle3D,
            bool hasMesh,
            bool hasDisplayRedraw,
            bool isAnimated)
        {
            if (mode != GraphicsMode.Enhanced)
                return ObjectPresentation.NativeBillboard;

            // Partial redraw coverage on animated sprites would flicker.
            bool redrawOk = hasDisplayRedraw && !isAnimated;

            if (toggle3D && hasMesh)
                return ObjectPresentation.Mesh;

            if (redrawOk) return ObjectPresentation.RedrawBillboard;
            return ObjectPresentation.NativeBillboard;
        }
    }
}
