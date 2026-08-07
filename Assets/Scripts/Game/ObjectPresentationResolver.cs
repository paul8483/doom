namespace Doom.Game
{
    /// Per-lump presentation for Classic / Enhanced ± 3D Objects toggle.
    public enum ObjectPresentation
    {
        NativeBillboard = 0,
        EdgeMixBillboard = 1,
        RedrawBillboard = 2,
        Mesh = 3,
    }

    /// Pure cascade from the Enhanced 3D Objects Toggle design matrix.
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

            if (toggle3D)
            {
                if (hasMesh) return ObjectPresentation.Mesh;
                if (redrawOk) return ObjectPresentation.RedrawBillboard;
                return ObjectPresentation.EdgeMixBillboard;
            }

            if (redrawOk) return ObjectPresentation.RedrawBillboard;
            return ObjectPresentation.EdgeMixBillboard;
        }
    }
}
