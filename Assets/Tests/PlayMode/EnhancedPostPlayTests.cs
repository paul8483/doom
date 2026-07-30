using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;

namespace Doom.Stage3.PlayTests
{
    public class EnhancedPostPlayTests
    {
        static IEnumerator WaitForMapBuild(float timeoutSeconds = 180f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var loader = Object.FindFirstObjectByType<MapLoader>();
                if (loader != null && loader.LastBuildSeconds > 0f)
                    yield break;
                yield return new WaitForSecondsRealtime(0.01f);
            }
            Assert.Fail("MapLoader build did not finish");
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            RenderSettings.fog = false;
            var gfx = GraphicsModeController.Instance;
            if (gfx != null)
            {
                gfx.SetCapabilities(GraphicsCapabilityPolicy.Probe());
                gfx.Apply(GraphicsMode.Classic);
            }
        }

        [UnityTest]
        public IEnumerator Classic_disables_volume_hdr_and_restores_baseline_scale()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return WaitForMapBuild();

            var gfx = GraphicsModeController.Ensure();
            Assert.IsNotNull(gfx.Context?.CameraRenderer);
            var cam = gfx.Context.CameraRenderer;

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            Assert.IsTrue(cam.PostProcessingEnabled);
            Assert.IsTrue(cam.VolumeEnabled);
            Assert.IsTrue(cam.WorldCamera.allowHDR);
            Assert.AreEqual(EnhancedPostController.EnhancedRenderScale, cam.Post.ActiveRenderScale, 0.001f);
            Assert.AreEqual(EnhancedPostController.EnhancedMsaaSamples, cam.Post.ActiveMsaa);

            float enhancedScale = cam.Post.ActiveRenderScale;

            gfx.Apply(GraphicsMode.Classic);
            yield return null;
            Assert.IsFalse(cam.PostProcessingEnabled);
            Assert.IsFalse(cam.VolumeEnabled);
            Assert.IsFalse(cam.WorldCamera.allowHDR);
            Assert.IsFalse(cam.WorldCamera.allowMSAA);
            Assert.IsFalse(RenderSettings.fog);
            Assert.AreEqual(1f, cam.Post.ActiveRenderScale, 0.001f);
            Assert.AreEqual(1, cam.Post.ActiveMsaa);

            // Re-enable Enhanced after "resize" notification — no stale Classic state.
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            cam.NotifyDisplayChanged();
            yield return null;
            Assert.IsTrue(cam.PostProcessingEnabled);
            Assert.IsTrue(cam.VolumeEnabled);
            Assert.AreEqual(enhancedScale, cam.Post.ActiveRenderScale, 0.001f);
        }

        [UnityTest]
        public IEnumerator Capability_gate_keeps_enhanced_mode_without_msaa_or_fsr()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return WaitForMapBuild();

            var gfx = GraphicsModeController.Ensure();
            // Full-suite order may leave the persistent controller Enhanced.
            // Force a real Classic→Enhanced apply so new capability gates bind.
            gfx.Apply(GraphicsMode.Classic);
            yield return null;
            gfx.SetCapabilities(new GraphicsCapabilityReport(
                msaa: false, renderScale: false, fsr: false));
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);

            Assert.AreEqual(GraphicsMode.Enhanced, gfx.Current);
            Assert.IsFalse(gfx.ActiveProfile.Msaa);
            Assert.IsFalse(gfx.ActiveProfile.RenderScaleOrFsr);
            Assert.IsTrue(gfx.Context.CameraRenderer.PostProcessingEnabled);

            var cam = gfx.Context.CameraRenderer;
            Assert.AreEqual(1f, cam.Post.ActiveRenderScale, 0.001f);
            Assert.AreEqual(1, cam.Post.ActiveMsaa);
            Assert.IsFalse(cam.WorldCamera.allowMSAA);
        }

        [UnityTest]
        public IEnumerator Enhanced_volume_has_bloom_and_grading_overrides()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return WaitForMapBuild();

            var gfx = GraphicsModeController.Ensure();
            Assert.IsNotNull(gfx.EnhancedVolumeProfile);
            Assert.IsTrue(gfx.EnhancedVolumeProfile.TryGet(out Bloom bloom));
            Assert.IsTrue(gfx.EnhancedVolumeProfile.TryGet(out ColorAdjustments grading));

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            Assert.IsTrue(bloom.active);
            Assert.IsTrue(grading.active);
            Assert.IsTrue(gfx.Context.CameraRenderer.Post.VolumeReady);
        }
    }
}
