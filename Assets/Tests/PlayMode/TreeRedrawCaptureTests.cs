using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;

namespace Doom.Stage3.PlayTests
{
    /// <summary>
    /// Headless render-capture diagnostic for the Enhanced 2D tree redraws
    /// (white-fringe hunt). Loads E1M1, switches to Enhanced + 3D Objects Off,
    /// frames the first TRE2 decoration with the player camera and renders it
    /// to Logs/tree-redraw-capture.png (MSAA 4 to match the in-game target).
    /// Capture-only tool like StairCaptureTests — analysis happens offline.
    /// </summary>
    public class TreeRedrawCaptureTests
    {
        static string LogsDir => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Logs"));

        MemorySettingsStorage memory;
        FakeDisplayAdapter display;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            MapLoader.MapNameOverride = null;
            GameFlowController.ResetForTests();
            GameFlowController.AutoStartPlaying = true;
            memory = new MemorySettingsStorage();
            display = new FakeDisplayAdapter();
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            MapLoader.MapNameOverride = null;
            GameFlowController.ResetForTests();
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Capture_tree_redraw_to_png()
        {
            Time.captureDeltaTime = 1f / 60f;
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 300; i++)
            {
                var flow = GameFlowController.Instance;
                if (flow != null && flow.State == GameFlowState.Playing &&
                    GameObject.Find("Player") != null)
                    break;
                yield return null;
            }

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display);
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            // Let the Enhanced warm + billboard material swap settle.
            for (int i = 0; i < 120; i++) yield return null;

            var pc = Object.FindAnyObjectByType<PlayerController>();
            if (pc != null) pc.enabled = false;

            GameObject tree = GameObject.Find("Thing_54_TRE2");
            Assert.That(tree, Is.Not.Null, "E1M1 should spawn a TRE2 (54) decoration");
            // The Enhanced 2D mode is gone: expose the redraw billboard by
            // hiding the mesh through the model's test seam instead.
            var treeModel = tree.GetComponent<ExperimentalPickupModel>();
            if (treeModel != null) treeModel.SetEnhancedForTest(false);

            var cam = Camera.main;
            Assert.That(cam, Is.Not.Null);

            // Eye-level pose ~4.5 m in front of the tree, aimed at its middle.
            Vector3 treePos = tree.transform.position;
            Vector3 eye = treePos + new Vector3(0f, 2.0f, -4.5f);
            cam.transform.SetPositionAndRotation(
                eye, Quaternion.LookRotation((treePos + Vector3.up * 1.8f) - eye));
            yield return null; yield return null;

            string path = Path.Combine(LogsDir, "tree-redraw-capture.png");
            var rt = new RenderTexture(1200, 900, 24) { antiAliasing = 4 };
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prevActive;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);
            Debug.Log($"[TreeRedrawCapture] wrote {path}");

            settings.SetGraphicsMode(GraphicsMode.Classic);
            yield return null;
        }
    }
}
