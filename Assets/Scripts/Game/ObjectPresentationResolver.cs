namespace Doom.Game
{
    /// Per-lump presentation for Classic / Enhanced.
    public enum ObjectPresentation
    {
        NativeBillboard = 0,
        RedrawBillboard = 2,
        Mesh = 3,
    }

    /// Pure cascade: Classic is always native; Enhanced routes mesh ->
    /// display redraw -> native per lump. The user-facing Enhanced 2D mode
    /// (the 3D Objects toggle, settings v3) was removed 2026-08-28 — one
    /// Enhanced to test, and the toggle's Off branch only ever showed the
    /// same fallbacks the cascade already serves. EdgeMix 8× removed
    /// 2026-08-08: lumps without AI assets stay native.
    public static class ObjectPresentationResolver
    {
        public static ObjectPresentation Resolve(
            GraphicsMode mode,
            bool hasMesh,
            bool hasDisplayRedraw,
            bool isAnimated)
        {
            if (mode != GraphicsMode.Enhanced)
                return ObjectPresentation.NativeBillboard;

            if (hasMesh)
                return ObjectPresentation.Mesh;

            // Partial redraw coverage on animated sprites would flicker.
            if (hasDisplayRedraw && !isAnimated)
                return ObjectPresentation.RedrawBillboard;
            return ObjectPresentation.NativeBillboard;
        }
    }
}
