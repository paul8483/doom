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
    /// Resolution order per item: GPU cache → <see cref="EnhancedVariantStore"/>
    /// → <see cref="EnhancedDiskCache"/> → compute.
    public sealed class EnhancedWarmScheduler : IDisposable
    {
        public const float DefaultFrameBudgetMs = 7f;

        readonly CancellationTokenSource cts = new CancellationTokenSource();

        public bool IsCancelled => cts.IsCancellationRequested;

        /// Jobs accepted into the last Warm call (for PlayMode assertions).
        public int LastJobsStarted { get; private set; }

        /// Results integrated on the main thread during the last Warm call
        /// (compute path only; store/disk hits are counted separately).
        public int LastJobsIntegrated { get; private set; }

        /// Session-store hits integrated during the last Warm call.
        public int LastStoreHits { get; private set; }

        /// Disk-pack hits integrated during the last Warm call.
        public int LastDiskHits { get; private set; }

        /// Cumulative compute jobs across Warm calls since the last
        /// <see cref="ResetCompletedStats"/> (MapLoader may Warm in two phases).
        public static int LastCompletedComputeJobs { get; private set; }

        /// Cumulative store hits across Warm calls since the last reset.
        public static int LastCompletedStoreHits { get; private set; }

        /// Cumulative disk hits across Warm calls since the last reset.
        public static int LastCompletedDiskHits { get; private set; }

        /// Monotonic progress samples recorded during the last Warm call.
        public IReadOnlyList<float> LastProgressSamples => progressSamples;
        readonly List<float> progressSamples = new List<float>(64);

        /// Warm coroutines currently in flight (main thread only). Caches use
        /// this to refuse synchronous main-thread Super-xBR while a warm runs —
        /// per-frame lazy builds (sprite billboards) would starve the frame
        /// loop and stretch the warm itself by an order of magnitude.
        public static int ActiveWarmCount { get; private set; }

        /// True while this scheduler's Warm holds an ActiveWarmCount slot.
        /// Unity kills coroutines on GameObject destroy WITHOUT running their
        /// finally blocks, so Dispose (always called by owners) must release
        /// the slot too — otherwise the count leaks and lazy builds stay
        /// native forever.
        bool holdsWarmSlot;

        void ReleaseWarmSlot()
        {
            if (!holdsWarmSlot) return;
            holdsWarmSlot = false;
            ActiveWarmCount = Math.Max(0, ActiveWarmCount - 1);
        }

        public static void ResetCompletedStats()
        {
            LastCompletedComputeJobs = 0;
            LastCompletedStoreHits = 0;
            LastCompletedDiskHits = 0;
            // Backstop for a slot leaked by a killed-without-Dispose scheduler:
            // callers reset stats only between warms, never mid-warm.
            ActiveWarmCount = 0;
        }

        public void Cancel()
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        public void Dispose()
        {
            Cancel();
            ReleaseWarmSlot();
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
            float frameBudgetMs = DefaultFrameBudgetMs,
            string wadIdentity = null,
            string wadPath = null)
        {
            LastJobsStarted = 0;
            LastJobsIntegrated = 0;
            LastStoreHits = 0;
            LastDiskHits = 0;
            progressSamples.Clear();

            // finally covers normal completion and enumerator Dispose; a
            // coroutine killed by GameObject destroy skips finally — that
            // path is covered by ReleaseWarmSlot in this scheduler's Dispose.
            holdsWarmSlot = true;
            ActiveWarmCount++;
            try
            {
                yield return WarmBody(
                    textures, sprites, hud, textureNames,
                    warmWorld, warmSprites, warmHud,
                    reportProgress, progressMin, progressMax, frameBudgetMs,
                    wadIdentity, wadPath);
            }
            finally
            {
                ReleaseWarmSlot();
            }
        }

        IEnumerator WarmBody(
            TextureCache textures,
            SpriteCache sprites,
            HudTextureCache hud,
            ICollection<string> textureNames,
            bool warmWorld,
            bool warmSprites,
            bool warmHud,
            Action<float, string> reportProgress,
            float progressMin,
            float progressMax,
            float frameBudgetMs,
            string wadIdentity,
            string wadPath)
        {
            var store = BindStore(wadIdentity);
            var disk = BindDisk(wadPath);
            if (disk != null)
            {
                yield return disk.WaitUntilLoaded();
                if (cts.IsCancellationRequested) yield break;
            }

            // Store keys come from each cache's StoreLayers — derived from the
            // same active profile its TryCreateJob reads (hot-switch pins the
            // target profile before warm), so keys always match built content.

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
                    textures, textureNames, frameBudgetMs, store, disk, textures.StoreLayers,
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
                    sprites, frameBudgetMs, store, disk, sprites.StoreLayers,
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
                    hud, frameBudgetMs, store, disk, hud.StoreLayers,
                    onItemDone: () =>
                    {
                        done++;
                        Report("ENHANCED HUD");
                    });
            }

            disk?.ScheduleFlush();
            // The jobs are done with the redraw PNG decodes; the store / pack
            // own the results from here on.
            WorldRedrawCatalog.ReleaseDecoded();
            HudRedrawCatalog.ReleaseDecoded();

            LastCompletedComputeJobs += LastJobsStarted;
            LastCompletedStoreHits += LastStoreHits;
            LastCompletedDiskHits += LastDiskHits;
        }

        static EnhancedVariantStore BindStore(string wadIdentity)
        {
            string identity = wadIdentity;
            if (string.IsNullOrEmpty(identity))
                identity = GameSessionHost.Instance != null
                    ? GameSessionHost.Instance.WadIdentity
                    : null;

            if (string.IsNullOrEmpty(identity))
                return null;

            var store = EnhancedVariantStore.Instance;
            store.BindWadIdentity(identity);
            return store;
        }

        static EnhancedDiskCache BindDisk(string wadPath)
        {
            if (!EnhancedDiskCache.Enabled)
                return null;

            string path = wadPath;
            if (string.IsNullOrEmpty(path))
                path = GameSessionHost.Instance != null
                    ? GameSessionHost.Instance.WadPath
                    : null;

            if (string.IsNullOrEmpty(path))
                return null;

            var disk = EnhancedDiskCache.Instance;
            disk.BindWad(path);
            return disk;
        }

        IEnumerator WarmWorld(
            TextureCache textures,
            ICollection<string> textureNames,
            float frameBudgetMs,
            EnhancedVariantStore store,
            EnhancedDiskCache disk,
            EnhancedLayerConfig layers,
            Action onItemDone)
        {
            var names = new List<string>(textureNames.Count);
            foreach (string name in textureNames)
            {
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }

            // Wave 1: albedo jobs (normals need integrated mips).
            float frameStart = Time.realtimeSinceStartup;
            var albedoItems = new List<JobItem>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (textures.HasEnhancedAlbedo(name))
                    continue;

                if (TryTakeCached(
                        store, disk, EnhancedJobKind.WorldAlbedo, name, layers,
                        out var cachedAlbedo, out bool fromDisk))
                {
                    textures.Integrate(name, cachedAlbedo);
                    if (fromDisk)
                    {
                        store?.Publish(EnhancedJobKind.WorldAlbedo, name, layers, cachedAlbedo);
                        LastDiskHits++;
                    }
                    else
                    {
                        LastStoreHits++;
                    }

                    // Cached-hit integration is a GPU upload — keep the loading
                    // plate responsive on an all-hits (disk/store) warm too.
                    if ((Time.realtimeSinceStartup - frameStart) * 1000f >= frameBudgetMs)
                    {
                        yield return null;
                        if (cts.IsCancellationRequested) yield break;
                        frameStart = Time.realtimeSinceStartup;
                    }

                    continue;
                }

                var job = textures.TryCreateAlbedoJob(name);
                if (job != null)
                {
                    string captured = name;
                    albedoItems.Add(new JobItem(
                        job,
                        r =>
                        {
                            textures.Integrate(captured, r);
                            if (r != null && r.Success)
                            {
                                store?.Publish(EnhancedJobKind.WorldAlbedo, captured, layers, r);
                                disk?.Publish(EnhancedJobKind.WorldAlbedo, captured, layers, r);
                            }
                        }));
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
            frameStart = Time.realtimeSinceStartup;
            var normalItems = new List<JobItem>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (textures.HasNormal(name))
                {
                    onItemDone?.Invoke();
                    continue;
                }

                if (TryTakeCached(
                        store, disk, EnhancedJobKind.WorldNormal, name, layers,
                        out var cachedNormal, out bool fromDisk))
                {
                    textures.Integrate(name, cachedNormal);
                    if (fromDisk)
                    {
                        store?.Publish(EnhancedJobKind.WorldNormal, name, layers, cachedNormal);
                        LastDiskHits++;
                    }
                    else
                    {
                        LastStoreHits++;
                    }

                    onItemDone?.Invoke();

                    if ((Time.realtimeSinceStartup - frameStart) * 1000f >= frameBudgetMs)
                    {
                        yield return null;
                        if (cts.IsCancellationRequested) yield break;
                        frameStart = Time.realtimeSinceStartup;
                    }

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
                            if (r != null && r.Success)
                            {
                                store?.Publish(EnhancedJobKind.WorldNormal, captured, layers, r);
                                disk?.Publish(EnhancedJobKind.WorldNormal, captured, layers, r);
                            }

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
            EnhancedVariantStore store,
            EnhancedDiskCache disk,
            EnhancedLayerConfig layers,
            Action onItemDone)
        {
            var lumps = sprites.CachedNativeLumps;
            float frameStart = Time.realtimeSinceStartup;
            var items = new List<JobItem>(lumps.Count);
            for (int i = 0; i < lumps.Count; i++)
            {
                int lump = lumps[i];
                if (sprites.HasEnhanced(lump))
                {
                    onItemDone?.Invoke();
                    continue;
                }

                string itemId = lump.ToString();
                var kind = sprites.EnhancedKindForLump(lump);
                if (TryTakeCached(
                        store, disk, kind, itemId, layers,
                        out var cached, out bool fromDisk))
                {
                    sprites.Integrate(lump, cached);
                    if (fromDisk)
                    {
                        store?.Publish(kind, itemId, layers, cached);
                        LastDiskHits++;
                    }
                    else
                    {
                        LastStoreHits++;
                    }

                    onItemDone?.Invoke();

                    if ((Time.realtimeSinceStartup - frameStart) * 1000f >= frameBudgetMs)
                    {
                        yield return null;
                        if (cts.IsCancellationRequested) yield break;
                        frameStart = Time.realtimeSinceStartup;
                    }

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
                        if (r != null && r.Success)
                        {
                            store?.Publish(kind, itemId, layers, r);
                            disk?.Publish(kind, itemId, layers, r);
                        }

                        onItemDone?.Invoke();
                    }));
            }

            yield return RunJobs(items, frameBudgetMs);
        }

        IEnumerator WarmHud(
            HudTextureCache hud,
            float frameBudgetMs,
            EnhancedVariantStore store,
            EnhancedDiskCache disk,
            EnhancedLayerConfig layers,
            Action onItemDone)
        {
            var names = new List<string>();
            foreach (string name in hud.HudPatchNames)
                names.Add(name);

            float frameStart = Time.realtimeSinceStartup;
            var items = new List<JobItem>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (hud.HasEnhanced(name))
                {
                    onItemDone?.Invoke();
                    continue;
                }

                if (TryTakeCached(
                        store, disk, EnhancedJobKind.Hud, name, layers,
                        out var cached, out bool fromDisk))
                {
                    hud.Integrate(name, cached);
                    if (fromDisk)
                    {
                        store?.Publish(EnhancedJobKind.Hud, name, layers, cached);
                        LastDiskHits++;
                    }
                    else
                    {
                        LastStoreHits++;
                    }

                    onItemDone?.Invoke();

                    if ((Time.realtimeSinceStartup - frameStart) * 1000f >= frameBudgetMs)
                    {
                        yield return null;
                        if (cts.IsCancellationRequested) yield break;
                        frameStart = Time.realtimeSinceStartup;
                    }

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
                        if (r != null && r.Success)
                        {
                            store?.Publish(EnhancedJobKind.Hud, captured, layers, r);
                            disk?.Publish(EnhancedJobKind.Hud, captured, layers, r);
                        }

                        onItemDone?.Invoke();
                    }));
            }

            yield return RunJobs(items, frameBudgetMs);
        }

        static bool TryTakeCached(
            EnhancedVariantStore store,
            EnhancedDiskCache disk,
            EnhancedJobKind kind,
            string itemId,
            EnhancedLayerConfig layers,
            out EnhancedJobResult result,
            out bool fromDisk)
        {
            result = null;
            fromDisk = false;

            if (store != null && store.TryGet(kind, itemId, layers, out result))
                return true;

            if (disk != null && disk.TryGet(kind, itemId, layers, out result))
            {
                fromDisk = true;
                return true;
            }

            return false;
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
                    });
                }
                catch (OperationCanceledException)
                {
                    // Expected on Cancel / scene teardown.
                }
                catch (Exception e)
                {
                    // Not expected (Run swallows job errors). Log and fall through
                    // so the integrate loop bails instead of waiting forever.
                    Debug.LogWarning($"EnhancedWarmScheduler: worker faulted: {e.Message}");
                }
            }, token);

            int integrated = 0;
            while (integrated < items.Count)
            {
                if (cts.IsCancellationRequested)
                    yield break;

                float frameStart = Time.realtimeSinceStartup;

                while (queue.TryDequeue(out var item))
                {
                    if (cts.IsCancellationRequested)
                        yield break;

                    item.integrate(item.result);
                    integrated++;
                    LastJobsIntegrated++;

                    if ((Time.realtimeSinceStartup - frameStart) * 1000f >= frameBudgetMs)
                        break;
                }

                if (integrated >= items.Count)
                    break;

                // Worker done and nothing left to drain: only reachable when the
                // worker faulted and dropped items — bail instead of spinning.
                if (worker.IsCompleted && queue.IsEmpty)
                    break;

                // Yield every frame so the loading plate stays responsive.
                yield return null;
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
