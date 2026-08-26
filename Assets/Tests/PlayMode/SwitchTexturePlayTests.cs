using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Wad;
using Doom.Map;
using Doom.Specials;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    /// P_ChangeSwitchTexture port: pressing a switch flips the front sidedef's
    /// SW1/SW2 texture on the LIVE map and rebuilds the wall; a repeatable
    /// switch pops back after BUTTONTIME.
    public class SwitchTexturePlayTests
    {
        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Switch_press_flips_texture_and_button_pops_back()
        {
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null;

            var activator = Object.FindAnyObjectByType<LineActivator>();
            Assert.That(activator, Is.Not.Null, "LineActivator should be on the Player");

            // Scan a fresh WAD copy for candidate lines (indices match the live map):
            // a Switch-trigger executable special whose action can actually fire
            // (tagged target exists / manual door has a back sector), carrying an
            // SW1/SW2 texture on its front side. Exits would end the level - skip.
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M1");

            int line = -1, sideIdx = -1;
            string from = null, to = null;
            var slot = SwitchTextureRules.Slot.None;
            bool repeatable = false;

            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (ld.Special == 0 || ld.FrontSideIdx < 0) continue;
                if (!LineSpecialTable.TryGet(ld.Special, out var sp)) continue;
                if (sp.Trigger != TriggerKind.Switch || !sp.IsExecutable) continue;
                if (sp.Category == SpecialCategory.Exit) continue;
                if (sp.Key != KeyKind.None) continue;

                bool canFire = false;
                if (ld.Tag != 0)
                {
                    foreach (var s in map.Sectors)
                        if (s.Tag == ld.Tag) { canFire = true; break; }
                }
                else if (sp.Category == SpecialCategory.Light)
                    canFire = true;
                else
                    canFire = ld.BackSideIdx >= 0;
                if (!canFire) continue;

                var side = map.SideDefs[ld.FrontSideIdx];
                var sl = SwitchTextureRules.FindSlot(side, out var f, out var t);
                if (sl == SwitchTextureRules.Slot.None) continue;

                line = i; sideIdx = ld.FrontSideIdx; slot = sl;
                from = f; to = t; repeatable = sp.Repeatable;
                break;
            }

            Assert.That(line, Is.GreaterThanOrEqualTo(0),
                "E1M1 should carry at least one usable switch with an SW texture");

            activator.ActivateLineForTest(line);
            yield return null;

            Assert.AreEqual(to, SlotTexture(activator.GetSideDefForTest(sideIdx), slot),
                $"line {line}: switch texture should flip {from} -> {to}");
            Assert.That(FindWallWithTexture(to), Is.Not.Null,
                $"a rebuilt wall named after {to} should exist in the scene");

            if (repeatable)
            {
                Assert.That(activator.ActiveButtonCountForTest, Is.GreaterThan(0),
                    "a repeatable switch should queue a button restore");
                yield return new WaitForSeconds(SwitchTextureRules.ButtonSeconds + 0.5f);
                Assert.AreEqual(from, SlotTexture(activator.GetSideDefForTest(sideIdx), slot),
                    "the button should pop back after BUTTONTIME");
                Assert.AreEqual(0, activator.ActiveButtonCountForTest);
            }
            else
            {
                Assert.AreEqual(0, activator.ActiveButtonCountForTest,
                    "a one-shot switch must not queue a restore");
            }
        }

        static string SlotTexture(in SideDef side, SwitchTextureRules.Slot slot) =>
            slot switch
            {
                SwitchTextureRules.Slot.Upper => side.UpperTexture,
                SwitchTextureRules.Slot.Middle => side.MiddleTexture,
                SwitchTextureRules.Slot.Lower => side.LowerTexture,
                _ => null,
            };

        static GameObject FindWallWithTexture(string texture)
        {
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (mr.gameObject.name.StartsWith("Wall_") &&
                    mr.gameObject.name.EndsWith("_" + texture) &&
                    mr.gameObject.activeInHierarchy)
                    return mr.gameObject;
            return null;
        }
    }
}
