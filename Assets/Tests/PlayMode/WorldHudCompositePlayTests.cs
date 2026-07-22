using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;

namespace Doom.Stage3.PlayTests
{
    public class WorldHudCompositePlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator World_post_toggles_without_affecting_virtual_screen_layout()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var classicLayout = VirtualScreenRenderer.Compute(1280, 720);
            var gfx = GraphicsModeController.Ensure();

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            Assert.IsTrue(gfx.Context.CameraRenderer.PostProcessingEnabled);

            var enhancedLayout = VirtualScreenRenderer.Compute(1280, 720);
            Assert.AreEqual(classicLayout.Scale, enhancedLayout.Scale, 0.0001f);
            Assert.AreEqual(classicLayout.OriginX, enhancedLayout.OriginX, 0.0001f);
            Assert.AreEqual(classicLayout.OriginY, enhancedLayout.OriginY, 0.0001f);

            // Aspect variants remain height-based. Same height → same scale;
            // wider screen gets larger (more positive) OriginX pillarbox.
            var wide = VirtualScreenRenderer.Compute(1920, 1080);
            var fourThree = VirtualScreenRenderer.Compute(1440, 1080);
            Assert.AreEqual(wide.Scale, fourThree.Scale, 0.0001f);
            Assert.Greater(wide.OriginX, fourThree.OriginX);

            gfx.Apply(GraphicsMode.Classic);
            yield return null;
            Assert.IsFalse(gfx.Context.CameraRenderer.PostProcessingEnabled);

            // Camera.Render is world-only — OnGUI HUD is intentionally not under Volume.
            var cam = Camera.main;
            Assert.IsNotNull(cam);
            Assert.IsFalse(cam.GetComponent<WorldCameraRenderer>().PostProcessingEnabled);
        }
    }
}
