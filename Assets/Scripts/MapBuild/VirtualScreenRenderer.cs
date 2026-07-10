using UnityEngine;

namespace Doom.MapBuild
{
    /// Shared 320×200 virtual-screen transform for weapon view and HUD.
    /// Scale matches the Stage 6c WeaponView formula (height-based, horizontally
    /// centered) so migrating DrawPatch does not change on-screen placement.
    public static class VirtualScreenRenderer
    {
        public const int Width = 320;
        public const int Height = 200;
        public const int StatusBarHeight = 32;

        public readonly struct Transform
        {
            /// Pixels per virtual pixel (Screen.height / 200).
            public readonly float Scale;
            /// Screen X of virtual X = 0.
            public readonly float OriginX;
            /// Screen Y of virtual Y = 0 (top).
            public readonly float OriginY;
            public readonly int ScreenWidth;
            public readonly int ScreenHeight;

            public Transform(float scale, float originX, float originY, int screenW, int screenH)
            {
                Scale = scale;
                OriginX = originX;
                OriginY = originY;
                ScreenWidth = screenW;
                ScreenHeight = screenH;
            }
        }

        /// Compute the transform for the current (or supplied) screen size.
        public static Transform Compute(int screenWidth, int screenHeight)
        {
            if (screenWidth < 1) screenWidth = 1;
            if (screenHeight < 1) screenHeight = 1;

            // Nearest-neighbour friendly: snap scale to a stable float from height.
            float scale = screenHeight / (float)Height;
            // Virtual x=160 maps to horizontal screen center (widescreen pillar of content).
            float originX = screenWidth * 0.5f - (Width * 0.5f) * scale;
            float originY = 0f;
            return new Transform(scale, originX, originY, screenWidth, screenHeight);
        }

        public static Transform ComputeForScreen() => Compute(Screen.width, Screen.height);

        /// Map a virtual-space rectangle to screen pixels.
        public static Rect ToScreen(in Transform t, float vx, float vy, float vw, float vh) =>
            new Rect(
                t.OriginX + vx * t.Scale,
                t.OriginY + vy * t.Scale,
                vw * t.Scale,
                vh * t.Scale);

        /// Integer-snapped screen rect (reduces shimmer on resize).
        public static Rect ToScreenSnapped(in Transform t, float vx, float vy, float vw, float vh)
        {
            float x = t.OriginX + vx * t.Scale;
            float y = t.OriginY + vy * t.Scale;
            float w = vw * t.Scale;
            float h = vh * t.Scale;
            int ix = Mathf.RoundToInt(x);
            int iy = Mathf.RoundToInt(y);
            int iw = Mathf.Max(1, Mathf.RoundToInt(w));
            int ih = Mathf.Max(1, Mathf.RoundToInt(h));
            return new Rect(ix, iy, iw, ih);
        }

        /// R_DrawPSprite: patch left/top in virtual px from sx/sy and DOOM offsets.
        public static Rect WeaponPatch(
            in Transform t, float sx, float sy,
            int leftOffset, int topOffset, int width, int height)
        {
            float left = sx - leftOffset;
            float top = sy - topOffset;
            return ToScreen(t, left, top, width, height);
        }
    }
}
