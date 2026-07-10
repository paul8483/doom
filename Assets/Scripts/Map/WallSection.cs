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
        public MeshData Mesh { get; }

        public WallSection(string texture, bool masked, MeshData mesh, bool blocks = true)
        {
            Texture = texture;
            Masked = masked;
            Blocks = blocks;
            Mesh = mesh;
        }
    }
}
