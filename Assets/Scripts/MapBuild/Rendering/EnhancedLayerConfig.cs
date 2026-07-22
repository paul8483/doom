using System;

namespace Doom.MapBuild.Rendering
{
    /// Profile layer flags that affect Enhanced CPU output. Part of the
    /// session/disk cache key so a capture with different layers cannot reuse
    /// a prior build (closes the "cache by name, layers from profile" hole).
    public readonly struct EnhancedLayerConfig : IEquatable<EnhancedLayerConfig>
    {
        public readonly bool WorldDedither;
        public readonly bool WorldUpscale4X;
        public readonly bool SpritesUpscale4X;
        public readonly bool UiUpscale4X;

        public EnhancedLayerConfig(
            bool worldDedither,
            bool worldUpscale4X,
            bool spritesUpscale4X,
            bool uiUpscale4X)
        {
            WorldDedither = worldDedither;
            WorldUpscale4X = worldUpscale4X;
            SpritesUpscale4X = spritesUpscale4X;
            UiUpscale4X = uiUpscale4X;
        }

        public static EnhancedLayerConfig FromProfile(GraphicsProfile profile) =>
            new EnhancedLayerConfig(
                profile.WorldDedither,
                profile.WorldUpscale4X,
                profile.SpritesUpscale4X,
                profile.UiUpscale4X);

        public bool Equals(EnhancedLayerConfig other) =>
            WorldDedither == other.WorldDedither
            && WorldUpscale4X == other.WorldUpscale4X
            && SpritesUpscale4X == other.SpritesUpscale4X
            && UiUpscale4X == other.UiUpscale4X;

        public override bool Equals(object obj) =>
            obj is EnhancedLayerConfig other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(WorldDedither, WorldUpscale4X, SpritesUpscale4X, UiUpscale4X);

        public static bool operator ==(EnhancedLayerConfig a, EnhancedLayerConfig b) => a.Equals(b);
        public static bool operator !=(EnhancedLayerConfig a, EnhancedLayerConfig b) => !a.Equals(b);
    }
}
