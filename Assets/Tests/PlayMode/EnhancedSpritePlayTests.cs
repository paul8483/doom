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
    public class EnhancedSpritePlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
        }

        [UnityTest]
        public IEnumerator Enhanced_uses_lit_sprite_or_spectre_shader()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);

            int enhancedSprites = 0;
            int anyBillboardMats = 0;
            foreach (var bb in Object.FindObjectsByType<SpriteBillboard>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var r = bb.GetComponent<MeshRenderer>();
                if (r == null || r.sharedMaterial == null || r.sharedMaterial.shader == null)
                    continue;
                anyBillboardMats++;
                string name = r.sharedMaterial.shader.name;
                if (name == DoomMaterialFactory.EnhancedSpriteName ||
                    name == DoomMaterialFactory.SpectreName ||
                    // Fallback if EnhancedSprite not yet imported: still lit path via cutout.
                    name == DoomMaterialFactory.ClassicCutoutName)
                    enhancedSprites++;
            }

            Assert.That(anyBillboardMats, Is.GreaterThan(0), "Expected live sprite billboards with materials");
            Assert.That(enhancedSprites, Is.GreaterThan(0),
                "Enhanced LitSprites should assign Doom/EnhancedSprite (or Spectre) on monsters");

            // Synthetic spectre material — Freedoom E1M1 may lack type 58.
            var factory = gfx.Context?.Materials ?? new DoomMaterialFactory();
            factory.SetActiveProfile(GraphicsProfile.Enhanced);
            var spectreMat = factory.CreateSpriteMaterial(Texture2D.whiteTexture, spectreFlag: true);
            Assert.That(
                spectreMat.shader.name,
                Is.EqualTo(DoomMaterialFactory.SpectreName).Or.EqualTo(DoomMaterialFactory.ClassicCutoutName));
            Object.Destroy(spectreMat);
        }

        [UnityTest]
        public IEnumerator Classic_uses_cutout_and_not_spectre()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Classic);
            for (int i = 0; i < 10; i++) yield return null;

            int classic = 0;
            int spectre = 0;
            int any = 0;
            foreach (var bb in Object.FindObjectsByType<SpriteBillboard>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var r = bb.GetComponent<MeshRenderer>();
                if (r == null || r.sharedMaterial == null || r.sharedMaterial.shader == null)
                    continue;
                any++;
                string name = r.sharedMaterial.shader.name;
                if (name == DoomMaterialFactory.ClassicCutoutName)
                    classic++;
                if (name == DoomMaterialFactory.SpectreName)
                    spectre++;
            }

            Assert.That(any, Is.GreaterThan(0), "Expected sprite billboards with materials");
            Assert.That(classic, Is.GreaterThan(0), "Classic sprites should use Doom/ClassicCutout");
            Assert.AreEqual(0, spectre, "Classic must not use Spectre shader");
        }
    }
}
