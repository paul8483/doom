using System.Collections.Generic;
using UnityEngine;
using Doom.Graphics;

namespace Doom.MapBuild.Rendering
{
    /// Loads display-grade HUD redraws (Resources/EnhancedHud/&lt;NAME&gt;.png)
    /// for allowlisted status-bar patches and hands them to the Enhanced HUD
    /// job as the finished 4× level (the Super-xBR slot). Main-thread only
    /// (Resources.Load). Invalid or missing files fall back per item.
    public static class HudRedrawCatalog
    {
        static readonly Dictionary<string, DecodedImage> cache = new();
        static readonly HashSet<string> rejected = new();
        static readonly Dictionary<string, DecodedImage> overrides = new();

        /// Test seam: inject a redraw for a patch name without Resources.
        /// Pass null image to remove. Overrides still go through size
        /// validation so the invalid-size fallback stays testable.
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

        /// Drop decoded images once a warm consumed them (see WorldRedrawCatalog).
        public static void ReleaseDecoded() => cache.Clear();

        /// True when a valid redraw exists for the patch at 4× the native
        /// size. Returns the decoded top-down RGBA image ready for the job.
        public static bool TryGet(string name, DecodedImage native, out DecodedImage redraw)
        {
            redraw = null;
            if (string.IsNullOrEmpty(name) || native == null) return false;

            if (overrides.TryGetValue(name, out var injected))
            {
                if (!SizeValid(injected, native)) return false;
                redraw = injected;
                return true;
            }

            if (!HudRedrawAllowlist.Contains(name)) return false;
            if (rejected.Contains(name)) return false;
            if (cache.TryGetValue(name, out redraw)) return true;

            var resource = Resources.Load<Texture2D>(
                HudRedrawAllowlist.ResourcesPath(name));
            if (resource == null)
            {
                Debug.LogWarning(
                    $"HudRedrawCatalog: missing EnhancedHud resource for {name} — Super-xBR fallback");
                rejected.Add(name);
                return false;
            }

            var decoded = WorldRedrawCatalog.ToDecodedTopDown(resource);
            Resources.UnloadAsset(resource);
            if (!SizeValid(decoded, native))
            {
                Debug.LogWarning(
                    $"HudRedrawCatalog: {name} redraw is {decoded.Width}x{decoded.Height}, " +
                    $"want {native.Width * HudRedrawAllowlist.Scale}x{native.Height * HudRedrawAllowlist.Scale} — Super-xBR fallback");
                rejected.Add(name);
                return false;
            }

            cache[name] = decoded;
            redraw = decoded;
            return true;
        }

        static bool SizeValid(DecodedImage redraw, DecodedImage native) =>
            redraw != null &&
            redraw.Width == native.Width * HudRedrawAllowlist.Scale &&
            redraw.Height == native.Height * HudRedrawAllowlist.Scale;
    }
}
