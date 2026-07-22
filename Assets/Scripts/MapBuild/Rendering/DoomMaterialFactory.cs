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
        public const string EnhancedSpriteName = "Doom/EnhancedSprite";
        public const string SpectreName = "Doom/Spectre";
        public const string FluidName = "Doom/Fluid";

        public const string BumpMapProperty = "_BumpMap";
        public const string BumpScaleProperty = "_BumpScale";
        public const string RoughnessProperty = "_Roughness";
        public const string EmissionProperty = "_EmissionStrength";
        public const string CutoffProperty = "_Cutoff";
        public const string SoftFloorFadeProperty = "_SoftFloorFade";
        public const string CrossFadeProperty = "_CrossFade";
        public const string CrossTexProperty = "_CrossTex";
        /// Fat-pixel texel-AA sampling in Enhanced world shaders (opaque/cutout).
        public const string TexelAaKeyword = "DOOM_TEXEL_AA";
        /// Parallax occlusion from height in _BumpMap.a (solid opaque only).
        public const string ParallaxKeyword = "DOOM_PARALLAX";
        public const string ParallaxAmplitudeProperty = "_ParallaxAmplitude";
        public const float SoftFloorFadeAmount = 0.08f;

        Shader classicOpaque;
        Shader classicCutout;
        Shader enhancedOpaque;
        Shader enhancedCutout;
        Shader enhancedSprite;
        Shader spectre;
        Shader fluid;
        bool resolved;

        GraphicsProfile active = GraphicsProfile.Classic;
        int worldAnisoLevel = 9;
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

        public void SetWorldAnisoLevel(int level) =>
            worldAnisoLevel = Mathf.Clamp(level, 1, 16);

        void EnsureShaders()
        {
            if (resolved) return;
            classicOpaque = Require(ClassicOpaqueName);
            classicCutout = Require(ClassicCutoutName);
            enhancedOpaque = Require(EnhancedOpaqueName);
            enhancedCutout = Require(EnhancedCutoutName);
            // Task 11–12 shaders: fall back so Classic world materials still load
            // if a new shader has not been imported yet.
            enhancedSprite = Shader.Find(EnhancedSpriteName) ?? classicCutout;
            spectre = Shader.Find(SpectreName) ?? classicCutout;
            fluid = Shader.Find(FluidName) ?? enhancedOpaque;
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

        public Shader FluidShader()
        {
            EnsureShaders();
            return fluid;
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

        public Shader SpriteShader(bool spectreFlag)
        {
            EnsureShaders();
            if (spectreFlag && active.SpectreMaterial)
                return spectre;
            if (active.Mode == GraphicsMode.Enhanced && active.LitSprites)
                return enhancedSprite;
            return classicCutout;
        }

        public Material CreateSpriteMaterial(Texture2D texture, bool spectreFlag)
        {
            var mat = new Material(SpriteShader(spectreFlag));
            mat.mainTexture = texture;
            ConfigureSpriteSurface(mat);
            return mat;
        }

        public void RetargetSpriteMaterial(Material material, bool spectreFlag)
        {
            if (material == null) return;
            var shader = SpriteShader(spectreFlag);
            if (material.shader != shader)
                material.shader = shader;
            ConfigureSpriteSurface(material);
        }

        void ConfigureSpriteSurface(Material material)
        {
            if (material.HasProperty(CutoffProperty))
                material.SetFloat(CutoffProperty, 0.5f);

            float soft = active.SoftFloorIntersection ? SoftFloorFadeAmount : 0f;
            if (material.HasProperty(SoftFloorFadeProperty))
                material.SetFloat(SoftFloorFadeProperty, soft);

            if (material.HasProperty(CrossFadeProperty))
                material.SetFloat(CrossFadeProperty, 0f);

            if (material.HasProperty(RoughnessProperty))
                material.SetFloat(RoughnessProperty, 0.85f);
            if (material.HasProperty(EmissionProperty))
                material.SetFloat(EmissionProperty, 0f);
        }

        void ConfigureSurface(Material material, Texture2D albedo, bool masked)
        {
            if (masked && material.HasProperty(CutoffProperty))
                material.SetFloat(CutoffProperty, 0.5f);

            // Texel-AA is Enhanced world albedo only; Classic shaders ignore the keyword.
            if (active.WorldTexelAA)
                material.EnableKeyword(TexelAaKeyword);
            else
                material.DisableKeyword(TexelAaKeyword);

            bool enhanced = active.UseLitMaterials && active.ProceduralNormals;
            MaterialSurfaceProfile profile = default;
            if (enhanced)
            {
                profile = surfaceLookup?.Invoke(albedo)
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

            // POM: solid opaque Enhanced only (not cutout/masked; amplitude 0 for fluid).
            bool parallax = enhanced && !masked && active.WorldParallax
                && profile.ParallaxAmplitude > 0f;
            if (parallax)
            {
                material.EnableKeyword(ParallaxKeyword);
                if (material.HasProperty(ParallaxAmplitudeProperty))
                    material.SetFloat(ParallaxAmplitudeProperty, profile.ParallaxAmplitude);
            }
            else
            {
                material.DisableKeyword(ParallaxKeyword);
                if (material.HasProperty(ParallaxAmplitudeProperty))
                    material.SetFloat(ParallaxAmplitudeProperty, 0f);
            }
        }

        public void ApplyFilterPolicy(Texture2D texture)
        {
            if (texture == null) return;
            // Normal maps always stay Bilinear; albedo follows profile.
            if (texture.name != null && texture.name.EndsWith("/Normal", StringComparison.Ordinal))
            {
                texture.filterMode = texture.mipmapCount > 1
                    ? FilterMode.Trilinear
                    : FilterMode.Bilinear;
                texture.anisoLevel = texture.mipmapCount > 1 ? worldAnisoLevel : 1;
                return;
            }

            bool controlledMips = active.ControlledWorldMipmaps && texture.mipmapCount > 1;
            texture.filterMode = controlledMips
                ? FilterMode.Trilinear
                : active.BilinearWorldFiltering ? FilterMode.Bilinear : FilterMode.Point;
            texture.anisoLevel = controlledMips ? worldAnisoLevel : 1;
        }

        public FilterMode WorldFilterMode =>
            active.BilinearWorldFiltering ? FilterMode.Bilinear : FilterMode.Point;
    }
}
