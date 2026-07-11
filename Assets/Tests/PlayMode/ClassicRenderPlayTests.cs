using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;

namespace Doom.Stage3.PlayTests
{
    public class ClassicRenderPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Classic_materials_use_urp_shaders_and_point_filtering()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            Assert.AreEqual(ColorSpace.Linear, QualitySettings.activeColorSpace,
                "Stage 8 requires Linear color space");
            Assert.IsNotNull(GraphicsSettings.defaultRenderPipeline,
                "URP pipeline asset must be assigned");

            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            Assert.That(renderers.Length, Is.GreaterThan(0));

            int classic = 0;
            foreach (var r in renderers)
            {
                var mat = r.sharedMaterial;
                if (mat == null || mat.shader == null) continue;
                string name = mat.shader.name;
                if (name != DoomMaterialFactory.ClassicOpaqueName &&
                    name != DoomMaterialFactory.ClassicCutoutName)
                    continue;

                classic++;
                Assert.That(name, Does.Not.Contain("Hidden/InternalErrorShader"),
                    "pink/error shader is a Stage 8 blocker");
                if (mat.mainTexture is Texture2D tex)
                    Assert.AreEqual(FilterMode.Point, tex.filterMode);
            }

            Assert.That(classic, Is.GreaterThan(0),
                "expected ClassicOpaque/ClassicCutout materials on map geometry");

            // Classic: no post on world camera.
            var worldCam = Object.FindFirstObjectByType<WorldCameraRenderer>();
            Assert.IsNotNull(worldCam);
            Assert.IsFalse(worldCam.PostProcessingEnabled);
        }
    }
}
