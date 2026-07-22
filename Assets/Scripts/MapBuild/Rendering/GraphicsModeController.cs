using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Doom.Game;
using Doom.MapBuild;

namespace Doom.MapBuild.Rendering
{
    /// Persistent graphics-mode adapter. Survives scene reload on GameSessionHost;
    /// rebinds to each map's WorldRenderContext.
    public sealed class GraphicsModeController : MonoBehaviour, IGraphicsModeAdapter
    {
        public static GraphicsModeController Instance { get; private set; }

        GraphicsMode current = GraphicsMode.Classic;
        GraphicsProfile activeProfile = GraphicsProfile.Classic;
        WorldRenderContext context;
        DoomMaterialFactory factory;
        GraphicsCapabilityReport capabilities = GraphicsCapabilityReport.Full;
        VolumeProfile enhancedVolumeProfile;
        string lastError;
        bool enhancedWarmComplete;
        bool isApplying;
        Coroutine applyRoutine;
        EnhancedWarmScheduler warmScheduler;

        public GraphicsMode Current => current;
        public GraphicsProfile ActiveProfile => activeProfile;
        public GraphicsCapabilityReport Capabilities => capabilities;
        public WorldRenderContext Context => context;
        public DoomMaterialFactory Factory => factory;
        public string LastError => lastError;

        /// True while a yielded Classic→Enhanced warm is in progress.
        public bool IsApplying => isApplying;

        /// Enhanced world/sprite/HUD variants for the active map are built.
        public bool EnhancedWarmComplete => enhancedWarmComplete;

        public static GraphicsModeController Ensure()
        {
            if (Instance != null) return Instance;
            var host = GameSessionHost.Ensure();
            var ctrl = host.GetComponent<GraphicsModeController>();
            if (ctrl == null) ctrl = host.gameObject.AddComponent<GraphicsModeController>();
            return ctrl;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            factory = new DoomMaterialFactory();
            enhancedVolumeProfile = LoadVolumeProfile();
            capabilities = GraphicsCapabilityPolicy.Probe();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static VolumeProfile LoadVolumeProfile()
        {
#if UNITY_EDITOR
            var fromSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                "Assets/Settings/Rendering/DoomEnhancedVolume.asset");
            if (fromSettings != null)
            {
                // Ensure bloom/grading overrides exist on the shared asset.
                var bootstrap = new EnhancedPostController();
                bootstrap.Bind(fromSettings);
                return fromSettings;
            }
#endif
            var runtime = ScriptableObject.CreateInstance<VolumeProfile>();
            var post = new EnhancedPostController();
            post.Bind(runtime);
            return runtime;
        }

        public void SetCapabilities(GraphicsCapabilityReport report)
        {
            capabilities = report;
            context?.CameraRenderer?.SetCapabilities(report);
        }

        public void SetEnhancedVolumeProfile(VolumeProfile profile) =>
            enhancedVolumeProfile = profile;

        public VolumeProfile EnhancedVolumeProfile => enhancedVolumeProfile;

        /// MapLoader calls this after yielded ENHANCED TEXTURES/SPRITES/HUD warm.
        public void NotifyEnhancedWarmComplete() => enhancedWarmComplete = true;

        void CancelWarm()
        {
            if (warmScheduler != null)
            {
                warmScheduler.Cancel();
                warmScheduler.Dispose();
                warmScheduler = null;
            }
        }

        /// Called by MapLoader after creating the world camera / materials.
        public void RegisterContext(WorldRenderContext worldContext)
        {
            // Cancel workers before stopping the coroutine so Integrate is skipped.
            CancelWarm();
            if (applyRoutine != null)
            {
                StopCoroutine(applyRoutine);
                applyRoutine = null;
                isApplying = false;
            }

            context?.Dispose();
            context = worldContext;
            enhancedWarmComplete = false;
            if (context == null) return;

            if (factory == null) factory = new DoomMaterialFactory();
            context.BindFactory(factory);
            capabilities = GraphicsCapabilityPolicy.Probe();
            context.CameraRenderer?.SetCapabilities(capabilities);

            // Sync re-apply: MapLoader warms world Enhanced before this when needed.
            ApplyInternal(current, force: true);
        }

        public void ClearContext()
        {
            CancelWarm();
            if (applyRoutine != null)
            {
                StopCoroutine(applyRoutine);
                applyRoutine = null;
                isApplying = false;
            }

            context?.Dispose();
            context = null;
            enhancedWarmComplete = false;
        }

        public void Apply(GraphicsMode mode)
        {
            if (isApplying) return;

            if (!GameSettingsData.IsDefinedGraphicsMode(mode))
                mode = GraphicsMode.Classic;

            // Super-xBR 4× must not run sync in one frame (freezes New Game / Options).
            if (mode == GraphicsMode.Enhanced &&
                context != null &&
                !enhancedWarmComplete)
            {
                applyRoutine = StartCoroutine(ApplyEnhancedWithWarmRoutine(mode));
                return;
            }

            ApplyInternal(mode, force: false);
        }

        IEnumerator ApplyEnhancedWithWarmRoutine(GraphicsMode mode)
        {
            isApplying = true;
            lastError = null;

            var loading = LoadingView.Ensure();
            bool showedLoading = false;
            if (loading != null && !loading.IsVisible)
            {
                var loader = Object.FindFirstObjectByType<MapLoader>();
                string map = loader != null ? loader.LoadedMapName : "";
                loading.Show(loader != null ? loader.HudTextures : null, map);
                showedLoading = true;
            }

            try
            {
                yield return WarmEnhancedAssets(loading);

                ApplyInternal(mode, force: true);
                if (current == mode)
                    enhancedWarmComplete = true;
            }
            finally
            {
                if (showedLoading && loading != null)
                    loading.Hide();
                isApplying = false;
                applyRoutine = null;
            }
        }

        IEnumerator WarmEnhancedAssets(LoadingView loading)
        {
            var cache = context?.TextureCache;
            if (cache == null) yield break;

            var names = new HashSet<string>(System.StringComparer.Ordinal);
            context.CollectTextureNames(names);
            names.Add(WadSkyRenderer.SkyTextureName);

            var anim = AnimatedSurfaceSystem.Instance;
            if (anim != null && anim.Catalog != null)
            {
                foreach (var seq in anim.Catalog.Sequences)
                {
                    if (seq.Frames == null) continue;
                    for (int i = 0; i < seq.Frames.Length; i++)
                        if (!string.IsNullOrEmpty(seq.Frames[i]))
                            names.Add(seq.Frames[i]);
                }
            }

            var loader = Object.FindFirstObjectByType<MapLoader>();
            CancelWarm();
            warmScheduler = new EnhancedWarmScheduler();

            yield return warmScheduler.Warm(
                cache,
                loader != null ? loader.Sprites : null,
                loader != null ? loader.HudTextures : null,
                names,
                warmWorld: true,
                warmSprites: loader != null && loader.Sprites != null,
                warmHud: loader != null && loader.HudTextures != null,
                reportProgress: (progress, label) =>
                {
                    if (loading != null && loading.IsVisible)
                        loading.SetProgress(progress, label);
                },
                progressMin: 0.05f,
                progressMax: 0.95f);

            if (warmScheduler != null && warmScheduler.IsCancelled)
                yield break;

            if (loading != null && loading.IsVisible)
                loading.SetProgress(0.95f, "ENHANCED READY");
        }

        void ApplyInternal(GraphicsMode mode, bool force)
        {
            lastError = null;
            if (!GameSettingsData.IsDefinedGraphicsMode(mode))
                mode = GraphicsMode.Classic;

            if (!force && mode == current && context != null &&
                context.Materials != null &&
                context.Materials.ActiveMode == mode)
            {
                current = mode;
                return;
            }

            var previous = current;
            var previousProfile = activeProfile;

            try
            {
                var requested = GraphicsProfile.ForMode(mode);
                var effective = GraphicsCapabilityPolicy.Apply(requested, capabilities);

                if (factory == null) factory = new DoomMaterialFactory();
                factory.SetActiveProfile(effective);

                context?.ApplyProfile(effective, factory);

                current = mode;
                activeProfile = effective;
            }
            catch (System.Exception ex)
            {
                lastError = ex.Message;
                Debug.LogError($"GraphicsModeController: failed to apply {mode}: {ex.Message}");
                // Roll back requested mode bookkeeping; leave materials as-is if mid-apply.
                current = previous;
                activeProfile = previousProfile;
                try
                {
                    factory?.SetActiveProfile(previousProfile);
                    context?.ApplyProfile(previousProfile, factory);
                }
                catch (System.Exception rollbackEx)
                {
                    Debug.LogError($"GraphicsModeController: rollback failed: {rollbackEx.Message}");
                }
            }
        }
    }
}
