using System.Collections.Generic;

namespace Doom.MapBuild
{
    /// Reusable byte buffers for the row-flip every texture upload does
    /// (DecodedImage is top-down, Unity wants bottom-up). Every cache used to
    /// allocate a fresh array per mip level of every texture — for the redraw
    /// set alone that is hundreds of megabytes of transient LOH garbage per
    /// warm. Buffers are pooled by exact length because SetPixelData /
    /// LoadRawTextureData copy synchronously and want the exact byte count.
    /// Main thread only (the callers are Unity texture APIs).
    public static class FlipScratch
    {
        static readonly Dictionary<int, byte[]> byLength = new();

        /// Returns a pooled buffer holding <paramref name="img"/> flipped
        /// vertically. Valid until the next call with the same length.
        public static byte[] Flipped(Doom.Graphics.DecodedImage img)
        {
            int w = img.Width, h = img.Height;
            int stride = w * 4;
            int length = img.Rgba.Length;
            if (!byLength.TryGetValue(length, out var buf))
            {
                buf = new byte[length];
                byLength[length] = buf;
            }
            for (int y = 0; y < h; y++)
                System.Array.Copy(img.Rgba, y * stride, buf, (h - 1 - y) * stride, stride);
            return buf;
        }

        /// Drop pooled buffers (scene teardown / tests).
        public static void Clear() => byLength.Clear();
    }
}
