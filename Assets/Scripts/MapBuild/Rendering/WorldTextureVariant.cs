using System;

namespace Doom.MapBuild.Rendering
{
    /// Presentation variant for world albedo / normal textures.
    public enum WorldTextureVariant
    {
        Native = 0,
        /// <summary>Scale2x 2× variant. Retained for enum numeric stability; no longer mapped by profiles.</summary>
        [Obsolete("Use Enhanced4X. Numeric value retained for stability; not created by GraphicsProfile mapping.")]
        Enhanced2X = 1,
        /// Super-xBR 4× (dedither → upscale → controlled mips) Enhanced world albedo.
        Enhanced4X = 2,
        // 3–5 were EdgeMix Pickup/Enemy/Weapon 8× variants, removed 2026-08-08.
        /// Gate-0 display-grade redraw (Resources/EnhancedSprites) on native quad.
        EnhancedDisplayRedraw = 6,
        /// First-person weapon display redraw (Resources/EnhancedWeapons,
        /// exactly 4× the native patch); placement stays native-header based.
        EnhancedWeaponRedraw = 7,
    }
}
