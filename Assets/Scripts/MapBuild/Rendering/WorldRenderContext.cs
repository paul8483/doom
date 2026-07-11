using System;
using System.Collections.Generic;
using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild.Rendering
{
    /// Owns runtime render resources for the active map scene.
    /// Survives mode switches; destroyed on map teardown.
    public sealed class WorldRenderContext : IDisposable
    {
        readonly List<Renderer> renderers = new List<Renderer>(1024);
        readonly List<Texture2D> textures = new List<Texture2D>(512);
        readonly List<(Material material, bool masked)> materials =
            new List<(Material, bool)>(512);
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>(64);

        public Camera WorldCamera { get; private set; }
        public WorldCameraRenderer CameraRenderer { get; private set; }
        public DoomMaterialFactory Materials { get; private set; }
        public WadSkyRenderer Sky { get; set; }
        public bool IsDisposed { get; private set; }

        public IReadOnlyList<Renderer> Renderers => renderers;
        public IReadOnlyList<Texture2D> Textures => textures;
        public int MaterialCount => materials.Count;
        public int TextureCount => textures.Count;

        public void BindFactory(DoomMaterialFactory factory) =>
            Materials = factory ?? throw new ArgumentNullException(nameof(factory));

        public void SetWorldCamera(Camera camera, WorldCameraRenderer cameraRenderer)
        {
            WorldCamera = camera;
            CameraRenderer = cameraRenderer;
        }

        public void RegisterRenderer(Renderer renderer)
        {
            if (renderer != null) renderers.Add(renderer);
        }

        public void RegisterTexture(Texture2D texture)
        {
            if (texture != null) textures.Add(texture);
        }

        public void RegisterMaterial(Material material, bool masked)
        {
            if (material != null) materials.Add((material, masked));
        }

        public void RegisterOwned(UnityEngine.Object obj)
        {
            if (obj != null) owned.Add(obj);
        }

        public void ApplyProfile(GraphicsProfile profile, DoomMaterialFactory factory)
        {
            if (IsDisposed) return;
            Materials = factory ?? Materials;
            if (Materials == null) return;

            Materials.SetActiveProfile(profile);

            for (int i = 0; i < materials.Count; i++)
            {
                var (mat, masked) = materials[i];
                if (mat == null) continue;
                Materials.RetargetMaterial(mat, masked);
            }

            for (int i = 0; i < textures.Count; i++)
            {
                var tex = textures[i];
                if (tex == null) continue;
                Materials.ApplyFilterPolicy(tex);
            }

            CameraRenderer?.ApplyProfile(profile);

            var lights = UnityEngine.Object.FindFirstObjectByType<RuntimeSectorLights>();
            lights?.NotifyProfileChanged();

            EnhancedLightSystem.Instance?.ApplyProfile(profile);
            AnimatedSurfaceSystem.Instance?.ApplyProfile(profile);

            var caps = GraphicsModeController.Instance != null
                ? GraphicsModeController.Instance.Capabilities
                : GraphicsCapabilityReport.Full;
            SectorFogSystem.Instance?.ApplyProfile(profile, caps);

            Sky?.ApplyProfile(profile);

            ParticleEffectPool.Instance?.ApplyProfile(profile);
            DecalEffectPool.Instance?.ApplyProfile(profile);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null)
                    UnityEngine.Object.Destroy(owned[i]);
            }

            owned.Clear();
            renderers.Clear();
            textures.Clear();
            materials.Clear();
            WorldCamera = null;
            CameraRenderer = null;
            Materials = null;
        }
    }
}
