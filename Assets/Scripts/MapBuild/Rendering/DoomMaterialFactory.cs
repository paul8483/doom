using System;
using UnityEngine;
using Doom.Game;
using Doom.Graphics;

namespace Doom.MapBuild.Rendering
{
    /// Resolves Classic/Enhanced shaders and applies filter / normal surface policy.
    public sealed class DoomMaterialFactory
    {
        public const string ClassicOpaqueName = "Doom/ClassicOpaque";
        public const string ClassicCutoutName = "Doom/ClassicCutout";
        public const string EnhancedOpaqueName = "Doom/EnhancedWorld";
        public const string EnhancedCutoutName = "Doom/EnhancedCutout";

        public const string BumpMapProperty = "_BumpMap";
        public const string BumpScaleProperty = "_BumpScale";
        public const string RoughnessProperty = "_Roughness";
        public const string EmissionProperty = "_EmissionStrength";
        public const string CutoffProperty = "_Cutoff";

        Shader classicOpaque;
        Shader classicCutout;
        Shader enhancedOpaque;
        Shader enhancedCutout;
        bool resolved;

        GraphicsProfile active = GraphicsProfile.Classic;
        Func<Texture2D, Texture2D> normalLookup;
        Func<Texture2D, MaterialSurfaceProfile> surfaceLookup;

        public GraphicsProfile ActiveProfile => active;
        public GraphicsMode ActiveMode => active.Mode;

        public DoomMaterialFactory()
        {
            // Lazy resolve: SettingsController may construct the factory during
            // domain reload before Shader.Find is ready.
        }

        public void SetNormalLookup(Func<Texture2D, Texture2D> lookup) =>
            normalLookup = lookup;

        public void SetSurfaceLookup(Func<Texture2D, MaterialSurfaceProfile> lookup) =>
            surfaceLookup = lookup;

        void EnsureShaders()
        {
            if (resolved) return;
            classicOpaque = Require(ClassicOpaqueName);
            classicCutout = Require(ClassicCutoutName);
            enhancedOpaque = Require(EnhancedOpaqueName);
            enhancedCutout = Require(EnhancedCutoutName);
            resolved = true;
        }

        static Shader Require(string name)
        {
            var s = Shader.Find(name);
            if (s == null)
                throw new InvalidOperationException(
                    $"Shader '{name}' not found. Add it to Always Included Shaders.");
            return s;
        }

        public void SetActiveProfile(GraphicsProfile profile) => active = profile;

        public Shader OpaqueShader()
        {
            EnsureShaders();
            if (active.UseLitMaterials)
                return enhancedOpaque;
            return classicOpaque;
        }

        public Shader CutoutShader()
        {
            EnsureShaders();
            if (active.UseLitMaterials)
                return enhancedCutout;
            return classicCutout;
        }

        public Material CreateMaterial(Texture2D texture, bool masked)
        {
            var mat = new Material(masked ? CutoutShader() : OpaqueShader());
            mat.mainTexture = texture;
            ConfigureSurface(mat, texture, masked);
            return mat;
        }

        public void RetargetMaterial(Material material, bool masked)
        {
            if (material == null) return;
            var shader = masked ? CutoutShader() : OpaqueShader();
            if (material.shader != shader)
                material.shader = shader;
            ConfigureSurface(material, material.mainTexture as Texture2D, masked);
        }

        void ConfigureSurface(Material material, Texture2D albedo, bool masked)
        {
            if (masked && material.HasProperty(CutoffProperty))
                material.SetFloat(CutoffProperty, 0.5f);

            bool enhanced = active.UseLitMaterials && active.ProceduralNormals;
            if (enhanced)
            {
                var profile = surfaceLookup?.Invoke(albedo)
                    ?? MaterialSurfaceProfile.For(MaterialSurfaceCategory.Unknown);
                if (material.HasProperty(BumpMapProperty))
                {
                    var normal = normalLookup?.Invoke(albedo);
                    material.SetTexture(
                        BumpMapProperty,
                        normal != null ? normal : Texture2D.normalTexture);
                }
                if (material.HasProperty(BumpScaleProperty))
                    material.SetFloat(BumpScaleProperty, 1f);
                if (material.HasProperty(RoughnessProperty))
                    material.SetFloat(RoughnessProperty, profile.Roughness);
                if (material.HasProperty(EmissionProperty))
                    material.SetFloat(EmissionProperty, profile.Emission);
            }
            else
            {
                if (material.HasProperty(BumpMapProperty))
                    material.SetTexture(BumpMapProperty, null);
                if (material.HasProperty(BumpScaleProperty))
                    material.SetFloat(BumpScaleProperty, 1f);
                if (material.HasProperty(RoughnessProperty))
                    material.SetFloat(RoughnessProperty, 0.75f);
                if (material.HasProperty(EmissionProperty))
                    material.SetFloat(EmissionProperty, 0f);
            }
        }

        public void ApplyFilterPolicy(Texture2D texture)
        {
            if (texture == null) return;
            // Normal maps always stay Bilinear; albedo follows profile.
            if (texture.name != null && texture.name.EndsWith("/Normal", StringComparison.Ordinal))
            {
                texture.filterMode = FilterMode.Bilinear;
                return;
            }

            texture.filterMode = active.BilinearWorldFiltering
                ? FilterMode.Bilinear
                : FilterMode.Point;
        }

        public FilterMode WorldFilterMode =>
            active.BilinearWorldFiltering ? FilterMode.Bilinear : FilterMode.Point;
    }
}
