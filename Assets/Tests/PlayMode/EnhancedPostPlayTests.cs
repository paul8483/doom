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
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            RenderSettings.fog = false;
        }

        [UnityTest]
        public IEnumerator Classic_disables_volume_hdr_and_restores_baseline_scale()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var gfx = GraphicsModeController.Ensure();
            Assert.IsNotNull(gfx.Context?.CameraRenderer);
            var cam = gfx.Context.CameraRenderer;

            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;
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
            gfx.Apply(GraphicsMode.Enhanced);
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
            for (int i = 0; i < 90; i++) yield return null;

            var gfx = GraphicsModeController.Ensure();
            gfx.SetCapabilities(new GraphicsCapabilityReport(
                msaa: false, renderScale: false, fsr: false));
            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;

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
            for (int i = 0; i < 90; i++) yield return null;

            var gfx = GraphicsModeController.Ensure();
            Assert.IsNotNull(gfx.EnhancedVolumeProfile);
            Assert.IsTrue(gfx.EnhancedVolumeProfile.TryGet(out Bloom bloom));
            Assert.IsTrue(gfx.EnhancedVolumeProfile.TryGet(out ColorAdjustments grading));

            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;
            Assert.IsTrue(bloom.active);
            Assert.IsTrue(grading.active);
            Assert.IsTrue(gfx.Context.CameraRenderer.Post.VolumeReady);
        }
    }
}
