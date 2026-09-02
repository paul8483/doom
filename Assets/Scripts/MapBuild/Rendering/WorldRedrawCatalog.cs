using System.Collections.Generic;
using UnityEngine;
using Doom.Graphics;

namespace Doom.MapBuild.Rendering
{
    /// Loads display-grade world redraws (Resources/EnhancedWorld/&lt;NAME&gt;.png)
    /// for allowlisted wall/flat textures and hands them to the Enhanced albedo
    /// job as pre-decoded level zero. Main-thread only (Resources.Load).
    /// Invalid or missing files fall back to the Super-xBR path per item.
    public static class WorldRedrawCatalog
    {
        static readonly Dictionary<string, DecodedImage> cache = new();
        static readonly HashSet<string> rejected = new();
        static readonly Dictionary<string, DecodedImage> overrides = new();

        /// Test seam: inject a redraw for a texture name without Resources.
        /// Pass null image to remove. Overrides still go through size validation
        /// so the invalid-size fallback stays testable.
        public static void SetOverrideForTests(string name, DecodedImage image)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (image == null) overrides.Remove(name);
            else overrides[name] = image;
        }

        public static void ClearForTests()
        {
            cache.Clear();
            rejected.Clear();
            overrides.Clear();
        }

        /// True when a valid redraw exists for the name at its authoring scale
        /// (WorldRedrawAllowlist.ScaleFor: 4×, SKY1 8×) times the native size.
        /// Returns the decoded top-down RGBA image ready for the job.
        public static bool TryGet(string name, DecodedImage native, out DecodedImage redraw)
        {
            redraw = null;
            if (string.IsNullOrEmpty(name) || native == null) return false;

            if (overrides.TryGetValue(name, out var injected))
            {
                if (!SizeValid(name, injected, native)) return false;
                redraw = injected;
                return true;
            }

            if (!WorldRedrawAllowlist.Contains(name)) return false;
            if (rejected.Contains(name)) return false;
            if (cache.TryGetValue(name, out redraw)) return true;

            var resource = Resources.Load<Texture2D>(
                WorldRedrawAllowlist.ResourcesPath(name));
            if (resource == null)
            {
                Debug.LogWarning(
                    $"WorldRedrawCatalog: missing EnhancedWorld resource for {name} — Super-xBR fallback");
                rejected.Add(name);
                return false;
            }

            var decoded = ToDecodedTopDown(resource);
            if (!SizeValid(name, decoded, native))
            {
                Debug.LogWarning(
                    $"WorldRedrawCatalog: {name} redraw is {decoded.Width}x{decoded.Height}, " +
                    $"want {native.Width * WorldRedrawAllowlist.ScaleFor(name)}x{native.Height * WorldRedrawAllowlist.ScaleFor(name)} — Super-xBR fallback");
                rejected.Add(name);
                return false;
            }

            cache[name] = decoded;
            redraw = decoded;
            return true;
        }

        static bool SizeValid(string name, DecodedImage redraw, DecodedImage native)
        {
            int scale = WorldRedrawAllowlist.ScaleFor(name);
            return redraw != null &&
                redraw.Width == native.Width * scale &&
                redraw.Height == native.Height * scale;
        }

        /// Shared with HudRedrawCatalog: decode a Resources texture into the
        /// job pipeline's top-down RGBA image.
        internal static DecodedImage ToDecodedTopDown(Texture2D tex)
        {
            // Resources textures may be non-readable; blit via RenderTexture then.
            Texture2D readable = tex;
            if (!tex.isReadable)
            {
                var rt = RenderTexture.GetTemporary(
                    tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                UnityEngine.Graphics.Blit(tex, rt);
                readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                readable.Apply(false);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }

            try
            {
                var pixels = readable.GetPixels32();
                int w = readable.width, h = readable.height;
                var rgba = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                {
                    // GetPixels32 rows run bottom-up; DecodedImage is top-down.
                    int srcRow = (h - 1 - y) * w;
                    int dstRow = y * w * 4;
                    for (int x = 0; x < w; x++)
                    {
                        var c = pixels[srcRow + x];
                        int di = dstRow + x * 4;
                        rgba[di] = c.r;
                        rgba[di + 1] = c.g;
                        rgba[di + 2] = c.b;
                        rgba[di + 3] = c.a;
                    }
                }
                return new DecodedImage(w, h, rgba);
            }
            finally
            {
                if (!ReferenceEquals(readable, tex))
                    Object.Destroy(readable);
            }
        }
    }
}
