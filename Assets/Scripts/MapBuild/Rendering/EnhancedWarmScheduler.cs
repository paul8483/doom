using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Doom.Graphics;
using Doom.MapBuild;

namespace Doom.MapBuild.Rendering
{
    /// Parallel Enhanced warm orchestrator. Workers run pure
    /// <see cref="EnhancedJobRunner"/> jobs; the main thread integrates GPU
    /// uploads with a per-frame time budget. Used by MapLoader load phases and
    /// GraphicsModeController hot-switch warm (single source of warm loops).
    public sealed class EnhancedWarmScheduler : IDisposable
    {
        public const float DefaultFrameBudgetMs = 7f;

        readonly CancellationTokenSource cts = new CancellationTokenSource();

        public bool IsCancelled => cts.IsCancellationRequested;

        /// Jobs accepted into the last Warm call (for PlayMode assertions).
        public int LastJobsStarted { get; private set; }

        /// Results integrated on the main thread during the last Warm call.
        public int LastJobsIntegrated { get; private set; }

        /// Monotonic progress samples recorded during the last Warm call.
        public IReadOnlyList<float> LastProgressSamples => progressSamples;
        readonly List<float> progressSamples = new List<float>(64);

        public void Cancel()
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        public void Dispose()
        {
            Cancel();
            cts.Dispose();
        }

        /// Warm world albedo+normals, sprites, and/or HUD in phase order.
        /// Progress is reported in [progressMin, progressMax] with phase labels
        /// ENHANCED TEXTURES / SPRITES / HUD.
        public IEnumerator Warm(
            TextureCache textures,
            SpriteCache sprites,
            HudTextureCache hud,
            ICollection<string> textureNames,
            bool warmWorld,
            bool warmSprites,
            bool warmHud,
            Action<float, string> reportProgress,
            float progressMin = 0f,
            float progressMax = 1f,
            float frameBudgetMs = DefaultFrameBudgetMs)
        {
            LastJobsStarted = 0;
            LastJobsIntegrated = 0;
            progressSamples.Clear();

            int total = 0;
            if (warmWorld && textures != null && textureNames != null)
                total += textureNames.Count;
            if (warmSprites && sprites != null)
                total += sprites.CachedNativeLumpCount;
            if (warmHud && hud != null)
            {
                foreach (var _ in hud.HudPatchNames)
                    total++;
            }

            total = Math.Max(1, total);
            int done = 0;

            void Report(string label)
            {
                float t = progressMin + (progressMax - progressMin) * done / (float)total;
                if (progressSamples.Count == 0 || t >= progressSamples[progressSamples.Count - 1])
                    progressSamples.Add(t);
                reportProgress?.Invoke(t, label);
            }

            if (warmWorld && textures != null && textureNames != null && textureNames.Count > 0)
            {
                yield return WarmWorld(
                    textures, textureNames, frameBudgetMs,
                    onItemDone: () =>
                    {
                        done++;
                        Report("ENHANCED TEXTURES");
                    });
                if (cts.IsCancellationRequested) yield break;
            }

            if (warmSprites && sprites != null)
            {
                yield return WarmSprites(
                    sprites, frameBudgetMs,
                    onItemDone: () =>
                    {
                        done++;
                        Report("ENHANCED SPRITES");
                    });
                if (cts.IsCancellationRequested) yield break;
            }

            if (warmHud && hud != null)
            {
                yield return WarmHud(
                    hud, frameBudgetMs,
                    onItemDone: () =>
                    {
                        done++;
                        Report("ENHANCED HUD");
                    });
            }
        }

        IEnumerator WarmWorld(
            TextureCache textures,
            ICollection<string> textureNames,
            float frameBudgetMs,
            Action onItemDone)
        {
            var names = new List<string>(textureNames.Count);
            foreach (string name in textureNames)
            {
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }

            // Wave 1: albedo jobs (normals need integrated mips).
            var albedoItems = new List<JobItem>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (textures.HasEnhancedAlbedo(name))
                    continue;

                var job = textures.TryCreateAlbedoJob(name);
                if (job != null)
                {
                    string captured = name;
                    albedoItems.Add(new JobItem(
                        job,
                        r => textures.Integrate(captured, r)));
                }
                else
                {
                    // Mips ready but not uploaded — finalize on main thread.
                    textures.IntegratePendingAlbedoMips(name);
                }
            }

            yield return RunJobs(albedoItems, frameBudgetMs);
            if (cts.IsCancellationRequested) yield break;

            // Wave 2: normals; each logical texture completes exactly once here.
            var normalItems = new List<JobItem>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (textures.HasNormal(name))
                {
                    onItemDone?.Invoke();
                    continue;
                }

                var job = textures.TryCreateNormalJob(name);
                if (job != null)
                {
                    string captured = name;
                    normalItems.Add(new JobItem(
                        job,
                        r =>
                        {
                            textures.Integrate(captured, r);
                            onItemDone?.Invoke();
                        }));
                }
                else
                {
                    // Failed albedo / no mips — still count the logical item.
                    onItemDone?.Invoke();
                }
            }

            yield return RunJobs(normalItems, frameBudgetMs);
        }

        IEnumerator WarmSprites(
            SpriteCache sprites,
            float frameBudgetMs,
            Action onItemDone)
        {
            var lumps = sprites.CachedNativeLumps;
            var items = new List<JobItem>(lumps.Count);
            for (int i = 0; i < lumps.Count; i++)
            {
                int lump = lumps[i];
                if (sprites.HasEnhanced(lump))
                {
                    onItemDone?.Invoke();
                    continue;
                }

                var job = sprites.TryCreateJob(lump);
                if (job == null)
                {
                    onItemDone?.Invoke();
                    continue;
                }

                int captured = lump;
                items.Add(new JobItem(
                    job,
                    r =>
                    {
                        sprites.Integrate(captured, r);
                        onItemDone?.Invoke();
                    }));
            }

            yield return RunJobs(items, frameBudgetMs);
        }

        IEnumerator WarmHud(
            HudTextureCache hud,
            float frameBudgetMs,
            Action onItemDone)
        {
            var names = new List<string>();
            foreach (string name in hud.HudPatchNames)
                names.Add(name);

            var items = new List<JobItem>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (hud.HasEnhanced(name))
                {
                    onItemDone?.Invoke();
                    continue;
                }

                var job = hud.TryCreateJob(name);
                if (job == null)
                {
                    onItemDone?.Invoke();
                    continue;
                }

                string captured = name;
                items.Add(new JobItem(
                    job,
                    r =>
                    {
                        hud.Integrate(captured, r);
                        onItemDone?.Invoke();
                    }));
            }

            yield return RunJobs(items, frameBudgetMs);
        }

        IEnumerator RunJobs(List<JobItem> items, float frameBudgetMs)
        {
            if (items == null || items.Count == 0)
                yield break;
            if (cts.IsCancellationRequested)
                yield break;

            LastJobsStarted += items.Count;

            var queue = new ConcurrentQueue<(Action<EnhancedJobResult> integrate, EnhancedJobResult result)>();
            var token = cts.Token;
            int produced = 0;

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1),
                CancellationToken = token,
            };

            Task worker = Task.Run(() =>
            {
                try
                {
                    Parallel.ForEach(items, options, item =>
                    {
                        var result = EnhancedJobRunner.Run(item.Job);
                        queue.Enqueue((item.Integrate, result));
                        Interlocked.Increment(ref produced);
                    });
                }
                catch (OperationCanceledException)
                {
                    // Expected on Cancel / scene teardown.
                }
            }, token);

            int integrated = 0;
            while (integrated < items.Count)
            {
                if (cts.IsCancellationRequested)
                    yield break;

                float frameStart = Time.realtimeSinceStartup;
                bool integratedThisFrame = false;

                while (queue.TryDequeue(out var item))
                {
                    if (cts.IsCancellationRequested)
                        yield break;

                    item.integrate(item.result);
                    integrated++;
                    LastJobsIntegrated++;
                    integratedThisFrame = true;

                    if ((Time.realtimeSinceStartup - frameStart) * 1000f >= frameBudgetMs)
                        break;
                }

                if (integrated >= items.Count)
                    break;

                // Yield every frame so the loading plate stays responsive; also
                // when the queue is empty while workers are still producing.
                if (!integratedThisFrame || integrated < items.Count)
                    yield return null;
            }

            while (!worker.IsCompleted)
            {
                if (cts.IsCancellationRequested)
                    yield break;
                yield return null;
            }

            // Drain anything still queued (idempotent Integrate; count only new work).
            if (cts.IsCancellationRequested)
                yield break;

            while (integrated < items.Count && queue.TryDequeue(out var leftover))
            {
                leftover.integrate(leftover.result);
                integrated++;
                LastJobsIntegrated++;
            }
        }

        readonly struct JobItem
        {
            public readonly EnhancedJob Job;
            public readonly Action<EnhancedJobResult> Integrate;

            public JobItem(EnhancedJob job, Action<EnhancedJobResult> integrate)
            {
                Job = job;
                Integrate = integrate;
            }
        }
    }
}
