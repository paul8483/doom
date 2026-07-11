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
    public class GraphicsModePlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
        }

        [UnityTest]
        public IEnumerator Hot_switch_preserves_player_state_and_retargets_filters()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            var health = player.GetComponent<PlayerHealth>();
            Assert.IsNotNull(health);
            int hpBefore = health.Health;
            Vector3 posBefore = player.transform.position;

            var gfx = GraphicsModeController.Ensure();
            Assert.IsNotNull(gfx.Context);

            Texture2D sampleTex = null;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.sharedMaterial?.mainTexture is Texture2D t)
                {
                    sampleTex = t;
                    break;
                }
            }
            Assert.IsNotNull(sampleTex);
            Assert.AreEqual(FilterMode.Point, sampleTex.filterMode);

            gfx.Apply(GraphicsMode.Enhanced);
            Assert.AreEqual(GraphicsMode.Enhanced, gfx.Current);
            Assert.AreEqual(FilterMode.Bilinear, sampleTex.filterMode);
            Assert.IsTrue(gfx.Context.CameraRenderer.PostProcessingEnabled);

            Assert.AreEqual(hpBefore, health.Health);
            Assert.AreEqual(posBefore.x, player.transform.position.x, 0.001f);
            Assert.AreEqual(posBefore.z, player.transform.position.z, 0.001f);

            gfx.Apply(GraphicsMode.Classic);
            Assert.AreEqual(GraphicsMode.Classic, gfx.Current);
            Assert.AreEqual(FilterMode.Point, sampleTex.filterMode);
            Assert.IsFalse(gfx.Context.CameraRenderer.PostProcessingEnabled);

            // Stress: switch repeatedly without growing registered texture count.
            int texCount = gfx.Context.TextureCount;
            for (int i = 0; i < 20; i++)
            {
                gfx.Apply(i % 2 == 0 ? GraphicsMode.Enhanced : GraphicsMode.Classic);
                yield return null;
            }
            Assert.AreEqual(texCount, gfx.Context.TextureCount);
            Assert.AreEqual(hpBefore, health.Health);
        }
    }
}
