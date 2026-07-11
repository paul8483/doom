using NUnit.Framework;
using UnityEngine;
using Doom.MapBuild.Rendering;

namespace Doom.Map.Tests
{
    public class EnhancedLightPoolTests
    {
        [Test]
        public void Select_respects_light_and_shadow_capacity()
        {
            var scores = new float[20];
            var wants = new bool[20];
            for (int i = 0; i < 20; i++)
            {
                scores[i] = 20 - i;
                wants[i] = true;
            }

            var active = new int[EnhancedLightPool.MaxLights];
            var shadows = new int[EnhancedLightPool.MaxShadows];
            EnhancedLightPool.Select(scores, wants, 20, active, out int ac, shadows, out int sc);

            Assert.AreEqual(EnhancedLightPool.MaxLights, ac);
            Assert.AreEqual(EnhancedLightPool.MaxShadows, sc);
            // Highest scores first.
            Assert.AreEqual(0, active[0]);
            Assert.AreEqual(1, active[1]);
        }

        [Test]
        public void Select_prefers_near_important_via_Score()
        {
            Vector3 cam = Vector3.zero;
            float near = EnhancedLightPool.Score(cam, new Vector3(1f, 0f, 0f), 1f);
            float far = EnhancedLightPool.Score(cam, new Vector3(50f, 0f, 0f), 1f);
            float farImportant = EnhancedLightPool.Score(cam, new Vector3(50f, 0f, 0f), 100f);
            Assert.Greater(near, far);
            Assert.Greater(farImportant, far);
        }

        [Test]
        public void Select_skips_non_shadow_candidates_for_shadow_budget()
        {
            var scores = new float[] { 10f, 9f, 8f, 7f };
            var wants = new bool[] { false, false, true, true };
            var active = new int[8];
            var shadows = new int[4];
            EnhancedLightPool.Select(scores, wants, 4, active, out int ac, shadows, out int sc);
            Assert.AreEqual(4, ac);
            Assert.AreEqual(2, sc);
            Assert.AreEqual(2, shadows[0]);
            Assert.AreEqual(3, shadows[1]);
        }

        [Test]
        public void Pool_ctor_allocates_fixed_capacity_only()
        {
            var root = new GameObject("pool_test_root");
            try
            {
                using var pool = new EnhancedLightPool(root.transform);
                Assert.AreEqual(EnhancedLightPool.MaxLights, pool.AllocatedSlots);
                Assert.AreEqual(0, pool.CountEnabled());
                pool.DisableAll();
                Assert.AreEqual(0, pool.CountEnabled());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
