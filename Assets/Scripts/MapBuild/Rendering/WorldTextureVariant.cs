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
        /// Experimental pickup-only EdgeMix 8× sprite texture.
        EnhancedPickup8X = 3,
    }
}
