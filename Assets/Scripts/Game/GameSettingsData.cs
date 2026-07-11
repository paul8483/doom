using System;

namespace Doom.Game
{
    /// Validated runtime preferences. No Unity types; no PlayerPrefs.
    public sealed class GameSettingsData : IEquatable<GameSettingsData>
    {
        public const int SchemaVersion = 2;
        /// First schema that stores preferences (pre-GraphicsMode).
        public const int FirstSupportedSchemaVersion = 1;

        /// Matches MapLoader inspector default.
        public const float DefaultSfxVolume = 1f;
        /// Matches MapLoader inspector default.
        public const float DefaultMusicVolume = 0.55f;
        /// Matches PlayerController inspector default.
        public const float DefaultMouseSensitivity = 0.1f;
        public const GraphicsMode DefaultGraphicsMode = GraphicsMode.Classic;

        public float SfxVolume { get; }
        public float MusicVolume { get; }
        public float MouseSensitivity { get; }
        public bool InvertY { get; }
        public bool Fullscreen { get; }
        /// 0×0 means "use current / desktop default".
        public int ResolutionWidth { get; }
        public int ResolutionHeight { get; }
        public GraphicsMode GraphicsMode { get; }

        public GameSettingsData(
            float sfxVolume,
            float musicVolume,
            float mouseSensitivity,
            bool invertY,
            bool fullscreen,
            int resolutionWidth,
            int resolutionHeight,
            GraphicsMode graphicsMode = DefaultGraphicsMode)
        {
            SfxVolume = sfxVolume;
            MusicVolume = musicVolume;
            MouseSensitivity = mouseSensitivity;
            InvertY = invertY;
            Fullscreen = fullscreen;
            ResolutionWidth = resolutionWidth;
            ResolutionHeight = resolutionHeight;
            GraphicsMode = graphicsMode;
        }

        public static GameSettingsData Defaults { get; } = new GameSettingsData(
            DefaultSfxVolume,
            DefaultMusicVolume,
            DefaultMouseSensitivity,
            invertY: false,
            fullscreen: true,
            resolutionWidth: 0,
            resolutionHeight: 0,
            DefaultGraphicsMode);

        public static bool TryCreate(
            float sfxVolume,
            float musicVolume,
            float mouseSensitivity,
            bool invertY,
            bool fullscreen,
            int resolutionWidth,
            int resolutionHeight,
            out GameSettingsData data,
            out string error) =>
            TryCreate(sfxVolume, musicVolume, mouseSensitivity, invertY, fullscreen,
                resolutionWidth, resolutionHeight, DefaultGraphicsMode, out data, out error);

        public static bool TryCreate(
            float sfxVolume,
            float musicVolume,
            float mouseSensitivity,
            bool invertY,
            bool fullscreen,
            int resolutionWidth,
            int resolutionHeight,
            GraphicsMode graphicsMode,
            out GameSettingsData data,
            out string error)
        {
            data = null;
            error = null;

            if (!IsFinite(sfxVolume) || !IsFinite(musicVolume) || !IsFinite(mouseSensitivity))
            {
                error = "Volumes and sensitivity must be finite numbers.";
                return false;
            }

            if (resolutionWidth < 0 || resolutionHeight < 0)
            {
                error = "Resolution dimensions must be non-negative.";
                return false;
            }

            if ((resolutionWidth == 0) != (resolutionHeight == 0))
            {
                error = "Resolution width and height must both be zero or both positive.";
                return false;
            }

            if (!IsDefinedGraphicsMode(graphicsMode))
            {
                error = "GraphicsMode must be Classic or Enhanced.";
                return false;
            }

            data = new GameSettingsData(
                Clamp01(sfxVolume),
                Clamp01(musicVolume),
                ClampSensitivity(mouseSensitivity),
                invertY,
                fullscreen,
                resolutionWidth,
                resolutionHeight,
                graphicsMode);
            return true;
        }

        /// Maps unknown/out-of-range stored ints to Classic.
        public static GraphicsMode NormalizeGraphicsMode(int raw) =>
            IsDefinedGraphicsMode((GraphicsMode)raw) ? (GraphicsMode)raw : GraphicsMode.Classic;

        public static bool IsDefinedGraphicsMode(GraphicsMode mode) =>
            mode == GraphicsMode.Classic || mode == GraphicsMode.Enhanced;

        public GameSettingsData WithSfxVolume(float v) =>
            Clone(sfx: Clamp01(RequireFinite(v, nameof(v))));

        public GameSettingsData WithMusicVolume(float v) =>
            Clone(music: Clamp01(RequireFinite(v, nameof(v))));

        public GameSettingsData WithMouseSensitivity(float v) =>
            Clone(sens: ClampSensitivity(RequireFinite(v, nameof(v))));

        public GameSettingsData WithInvertY(bool v) => Clone(invert: v);
        public GameSettingsData WithFullscreen(bool v) => Clone(fs: v);
        public GameSettingsData WithGraphicsMode(GraphicsMode mode)
        {
            if (!IsDefinedGraphicsMode(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            return Clone(gfx: mode);
        }

        public GameSettingsData WithResolution(int width, int height)
        {
            if (width < 0 || height < 0)
                throw new ArgumentOutOfRangeException("Resolution must be non-negative.");
            if ((width == 0) != (height == 0))
                throw new ArgumentException("Resolution width/height must both be zero or both positive.");
            return Clone(rw: width, rh: height);
        }

        GameSettingsData Clone(
            float? sfx = null,
            float? music = null,
            float? sens = null,
            bool? invert = null,
            bool? fs = null,
            int? rw = null,
            int? rh = null,
            GraphicsMode? gfx = null) =>
            new GameSettingsData(
                sfx ?? SfxVolume,
                music ?? MusicVolume,
                sens ?? MouseSensitivity,
                invert ?? InvertY,
                fs ?? Fullscreen,
                rw ?? ResolutionWidth,
                rh ?? ResolutionHeight,
                gfx ?? GraphicsMode);

        static float RequireFinite(float v, string name)
        {
            if (!IsFinite(v))
                throw new ArgumentOutOfRangeException(name, "Value must be finite.");
            return v;
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        static float ClampSensitivity(float v)
        {
            if (v < 0.01f) return 0.01f;
            if (v > 2f) return 2f;
            return v;
        }

        public bool Equals(GameSettingsData other)
        {
            if (other is null) return false;
            return SfxVolume.Equals(other.SfxVolume)
                   && MusicVolume.Equals(other.MusicVolume)
                   && MouseSensitivity.Equals(other.MouseSensitivity)
                   && InvertY == other.InvertY
                   && Fullscreen == other.Fullscreen
                   && ResolutionWidth == other.ResolutionWidth
                   && ResolutionHeight == other.ResolutionHeight
                   && GraphicsMode == other.GraphicsMode;
        }

        public override bool Equals(object obj) => Equals(obj as GameSettingsData);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(SfxVolume);
            hash.Add(MusicVolume);
            hash.Add(MouseSensitivity);
            hash.Add(InvertY);
            hash.Add(Fullscreen);
            hash.Add(ResolutionWidth);
            hash.Add(ResolutionHeight);
            hash.Add(GraphicsMode);
            return hash.ToHashCode();
        }
    }
}
