using System;
using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild.Rendering
{
    /// Resolves Classic/Enhanced shaders and applies filter policy.
    /// Enhanced world shaders land in Task 7 — until then Enhanced reuses Classic
    /// unlit shaders while other profile flags (filter/HDR/post) still apply.
    public sealed class DoomMaterialFactory
    {
        public const string ClassicOpaqueName = "Doom/ClassicOpaque";
        public const string ClassicCutoutName = "Doom/ClassicCutout";
        public const string EnhancedOpaqueName = "Doom/EnhancedWorld";
        public const string EnhancedCutoutName = "Doom/EnhancedCutout";

        Shader classicOpaque;
        Shader classicCutout;
        Shader enhancedOpaque;
        Shader enhancedCutout;
        bool resolved;

        GraphicsProfile active = GraphicsProfile.Classic;

        public GraphicsProfile ActiveProfile => active;
        public GraphicsMode ActiveMode => active.Mode;

        public DoomMaterialFactory()
        {
            // Lazy resolve: SettingsController may construct the factory during
            // domain reload before Shader.Find is ready.
        }

        void EnsureShaders()
        {
            if (resolved) return;
            classicOpaque = Require(ClassicOpaqueName);
            classicCutout = Require(ClassicCutoutName);
            enhancedOpaque = Shader.Find(EnhancedOpaqueName);
            enhancedCutout = Shader.Find(EnhancedCutoutName);
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
            if (active.UseLitMaterials && enhancedOpaque != null)
                return enhancedOpaque;
            return classicOpaque;
        }

        public Shader CutoutShader()
        {
            EnsureShaders();
            if (active.UseLitMaterials && enhancedCutout != null)
                return enhancedCutout;
            return classicCutout;
        }

        public Material CreateMaterial(Texture2D texture, bool masked)
        {
            var mat = new Material(masked ? CutoutShader() : OpaqueShader());
            mat.mainTexture = texture;
            if (masked && mat.HasProperty("_Cutoff"))
                mat.SetFloat("_Cutoff", 0.5f);
            return mat;
        }

        public void RetargetMaterial(Material material, bool masked)
        {
            if (material == null) return;
            var shader = masked ? CutoutShader() : OpaqueShader();
            if (material.shader != shader)
                material.shader = shader;
            if (masked && material.HasProperty("_Cutoff"))
                material.SetFloat("_Cutoff", 0.5f);
        }

        public void ApplyFilterPolicy(Texture2D texture)
        {
            if (texture == null) return;
            texture.filterMode = active.BilinearWorldFiltering
                ? FilterMode.Bilinear
                : FilterMode.Point;
        }

        public FilterMode WorldFilterMode =>
            active.BilinearWorldFiltering ? FilterMode.Bilinear : FilterMode.Point;
    }
}
