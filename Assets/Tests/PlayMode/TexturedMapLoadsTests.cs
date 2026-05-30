using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace Doom.Stage3.PlayTests
{
    public class TexturedMapLoadsTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator E1M1_builds_with_textured_materials()
        {
            // Same degenerate-mesh warnings as the floor test; suppress so they
            // don't fail the run before we can assert.
            LogAssert.ignoreFailingMessages = true;

            // Pin delta-time so auto-bootstrap + Build() have deterministic frames.
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);

            // Give MapLoader.Start → Build() enough frames to finish.
            for (int i = 0; i < 90; i++) yield return null;

            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            Assert.That(renderers.Length, Is.GreaterThan(0), "map should produce mesh renderers");

            int doomTextured = 0;
            foreach (var r in renderers)
            {
                var mat = r.sharedMaterial;
                if (mat == null) continue;
                if (mat.shader != null && mat.shader.name.StartsWith("Doom/") && mat.mainTexture != null)
                    doomTextured++;
            }

            Debug.Log($"[PlayTest] Total renderers={renderers.Length} doomTextured={doomTextured}");

            Assert.That(doomTextured, Is.GreaterThan(0),
                "at least some meshes must use a Doom textured material");
        }
    }
}
