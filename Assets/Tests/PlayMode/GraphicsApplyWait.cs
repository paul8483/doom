using System.Collections;
using NUnit.Framework;
using Doom.Game;
using Doom.MapBuild.Rendering;

namespace Doom.Stage3.PlayTests
{
    /// Classic→Enhanced may yield Super-xBR warm under a loading plate.
    static class GraphicsApplyWait
    {
        public static IEnumerator Apply(
            GraphicsModeController gfx, GraphicsMode mode, int maxFrames = 20000)
        {
            Assert.IsNotNull(gfx);
            gfx.Apply(mode);
            for (int i = 0; i < maxFrames && gfx.IsApplying; i++)
                yield return null;
            Assert.IsFalse(gfx.IsApplying,
                $"GraphicsMode {mode} warm did not finish in {maxFrames} frames");
        }
    }
}
