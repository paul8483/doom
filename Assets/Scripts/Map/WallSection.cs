namespace Doom.Map
{
    /// One run of wall geometry that shares a single texture, so the glue can
    /// assign exactly one material per section. Masked = two-sided middle (cutout).
    /// Blocks = player/monster collider (always for solid walls; ML_BLOCKING for middles).
    public sealed class WallSection
    {
        public const ushort FlagBlocking = 0x0001;

        public string Texture { get; }
        public bool Masked { get; }
        public bool Blocks { get; }
        /// Linedef special shared by this section. Geometry with different specials
        /// is kept in separate buckets so renderer-level effects (for example wall
        /// scrolling) never leak onto neighbouring walls that reuse the texture.
        public int LineSpecial { get; }
        public MeshData Mesh { get; }

        public WallSection(string texture, bool masked, MeshData mesh, bool blocks = true,
                           int lineSpecial = 0)
        {
            Texture = texture;
            Masked = masked;
            Blocks = blocks;
            LineSpecial = lineSpecial;
            Mesh = mesh;
        }
    }
}
