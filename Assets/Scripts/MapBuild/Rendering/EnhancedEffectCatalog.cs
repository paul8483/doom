using UnityEngine;

namespace Doom.MapBuild.Rendering
{
    public enum EffectKind
    {
        Muzzle,
        Puff,
        Blood,
        Explosion,
        ProjectileTrail,
    }

    /// Presentation-only palette-ish colors and optional WAD texture name hints.
    /// No WAD I/O at catalog level.
    public static class EnhancedEffectCatalog
    {
        public static Color ColorFor(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.Muzzle:
                    return new Color(1.00f, 0.85f, 0.45f, 1f);
                case EffectKind.Puff:
                    return new Color(0.72f, 0.72f, 0.72f, 1f);
                case EffectKind.Blood:
                    return new Color(0.75f, 0.08f, 0.08f, 1f);
                case EffectKind.Explosion:
                    return new Color(1.00f, 0.45f, 0.12f, 1f);
                case EffectKind.ProjectileTrail:
                    return new Color(1.00f, 0.55f, 0.20f, 1f);
                default:
                    return Color.white;
            }
        }

        public static string TextureHint(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.Puff: return "PUFF";
                case EffectKind.Blood: return "BLUD";
                case EffectKind.Explosion: return "MISL";
                default: return null;
            }
        }

        public static float Lifetime(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.Muzzle: return 0.12f;
                case EffectKind.Puff: return 0.35f;
                case EffectKind.Blood: return 0.40f;
                case EffectKind.Explosion: return 0.55f;
                case EffectKind.ProjectileTrail: return 0.25f;
                default: return 0.3f;
            }
        }
    }
}
