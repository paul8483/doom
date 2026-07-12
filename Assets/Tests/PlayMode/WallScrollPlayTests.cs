using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;
using Doom.Map;
using Doom.Wad;
using Doom.Game;

namespace Doom.Stage3.PlayTests
{
    public class WallScrollPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            MapLoader.MapNameOverride = null;
            Time.captureDeltaTime = 0f;
        }

        [UnityTest]
        public IEnumerator Controller_advances_normalized_U_without_instancing_material()
        {
            var go = new GameObject("ScrollingWallTest");
            var renderer = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Doom/Unlit") ?? Shader.Find("Unlit/Texture");
            Assert.IsNotNull(shader);
            var material = new Material(shader);
            var texture = new Texture2D(70, 1);
            material.mainTexture = texture;
            renderer.sharedMaterial = material;
            var sharedBefore = renderer.sharedMaterial;

            var scroll = go.AddComponent<WallScrollController>();
            scroll.Configure(renderer, 48);
            scroll.ApplyTicForTest(35);

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Vector4 st = block.GetVector(Shader.PropertyToID("_MainTex_ST"));
            Assert.That(st.z, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.AreSame(sharedBefore, renderer.sharedMaterial);

            Object.Destroy(go);
            Object.Destroy(material);
            Object.Destroy(texture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Real_E1_scroll_binding_survives_graphics_profile_switch()
        {
            string path = System.IO.Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            string mapWithScroll = null;
            using (var wad = WadFile.Open(path))
            {
                for (int n = 1; n <= 9 && mapWithScroll == null; n++)
                {
                    string candidate = $"E1M{n}";
                    var map = MapData.Load(wad, candidate);
                    foreach (var line in map.LineDefs)
                    {
                        if (line.Special != 48 && line.Special != 85) continue;
                        mapWithScroll = candidate;
                        break;
                    }
                }
            }
            Assert.IsNotNull(mapWithScroll);

            MapLoader.MapNameOverride = mapWithScroll;
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var scrolls = Object.FindObjectsByType<WallScrollController>(
                FindObjectsSortMode.None);
            Assert.That(scrolls.Length, Is.GreaterThan(0));
            var scroll = scrolls[0];
            var renderer = scroll.GetComponent<MeshRenderer>();
            var block = new MaterialPropertyBlock();

            scroll.ApplyTicForTest(35);
            renderer.GetPropertyBlock(block);
            float classicOffset = block.GetVector(
                Shader.PropertyToID("_MainTex_ST")).z;

            GraphicsModeController.Ensure().Apply(GraphicsMode.Enhanced);
            scroll.ApplyTicForTest(35);
            renderer.GetPropertyBlock(block);
            float enhancedOffset = block.GetVector(
                Shader.PropertyToID("_MainTex_ST")).z;

            Assert.That(enhancedOffset, Is.EqualTo(classicOffset).Within(0.0001f));
        }
    }
}
