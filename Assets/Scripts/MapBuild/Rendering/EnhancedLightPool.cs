using System;
using UnityEngine;

namespace Doom.MapBuild.Rendering
{
        /// Fixed-capacity Unity Light pool. Capacity matches Stage 8 budgets
        /// (raised from 8→16 so busy COLU rooms like E1M7 still light nearby lamps).
        public sealed class EnhancedLightPool : IDisposable
        {
            public const int MaxLights = 16;
            public const int MaxShadows = 4;

        readonly Transform root;
        readonly Light[] lights;
        bool disposed;

        public int Capacity => MaxLights;
        public int ShadowCapacity => MaxShadows;
        public int AllocatedSlots => lights.Length;

        public EnhancedLightPool(Transform parent)
        {
            var go = new GameObject("EnhancedLightPool");
            go.transform.SetParent(parent, false);
            root = go.transform;
            lights = new Light[MaxLights];

            for (int i = 0; i < MaxLights; i++)
            {
                var child = new GameObject($"PooledLight_{i}");
                child.transform.SetParent(root, false);
                var light = child.AddComponent<Light>();
                light.type = LightType.Point;
                light.enabled = false;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel;
                lights[i] = light;
            }
        }

        /// Score for nearest/importance selection. Higher wins.
        public static float Score(Vector3 cameraPos, Vector3 lightPos, float importance)
        {
            float distSq = (cameraPos - lightPos).sqrMagnitude;
            return importance * (1f / (1f + distSq));
        }

        /// Selects up to MaxLights candidates by descending score; among selected,
        /// up to MaxShadows that want shadows. Writes candidate indices into outs.
        public static void Select(
            float[] scores,
            bool[] wantsShadow,
            int candidateCount,
            int[] activeOut,
            out int activeCount,
            int[] shadowOut,
            out int shadowCount)
        {
            if (scores == null) throw new ArgumentNullException(nameof(scores));
            if (wantsShadow == null) throw new ArgumentNullException(nameof(wantsShadow));
            if (activeOut == null) throw new ArgumentNullException(nameof(activeOut));
            if (shadowOut == null) throw new ArgumentNullException(nameof(shadowOut));

            activeCount = 0;
            shadowCount = 0;
            int n = Math.Min(candidateCount, Math.Min(scores.Length, wantsShadow.Length));
            if (n <= 0) return;

            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = 0; i < n - 1; i++)
            {
                int best = i;
                for (int j = i + 1; j < n; j++)
                    if (scores[order[j]] > scores[order[best]]) best = j;
                (order[i], order[best]) = (order[best], order[i]);
            }

            int take = Math.Min(MaxLights, n);
            for (int i = 0; i < take; i++)
            {
                int idx = order[i];
                if (scores[idx] <= 0f) break;
                if (activeCount < activeOut.Length)
                    activeOut[activeCount++] = idx;
            }

            for (int i = 0; i < activeCount && shadowCount < MaxShadows; i++)
            {
                int idx = activeOut[i];
                if (!wantsShadow[idx]) continue;
                if (shadowCount < shadowOut.Length)
                    shadowOut[shadowCount++] = idx;
            }
        }

        public Light Get(int slot) =>
            slot >= 0 && slot < lights.Length ? lights[slot] : null;

        /// Bind the first <paramref name="count"/> lights to the given params; disable the rest.
        public void ApplyFrame(
            Vector3[] positions,
            Color[] colors,
            float[] intensities,
            float[] ranges,
            bool[] shadows,
            int count)
        {
            int n = Math.Min(count, lights.Length);
            for (int i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (i >= n)
                {
                    light.enabled = false;
                    light.shadows = LightShadows.None;
                    continue;
                }

                light.transform.position = positions[i];
                light.color = colors[i];
                light.intensity = intensities[i];
                light.range = ranges[i];
                light.shadows = shadows[i] ? LightShadows.Hard : LightShadows.None;
                light.enabled = true;
            }
        }

        public void DisableAll()
        {
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                lights[i].enabled = false;
                lights[i].shadows = LightShadows.None;
            }
        }

        public int CountEnabled()
        {
            int n = 0;
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null && lights[i].enabled) n++;
            return n;
        }

        public int CountShadows()
        {
            int n = 0;
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null && lights[i].enabled &&
                    lights[i].shadows != LightShadows.None)
                    n++;
            return n;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(root.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(root.gameObject);
        }
    }
}
