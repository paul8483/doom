namespace Doom.Map
{
    /// One run of wall geometry that shares a single texture, so the glue can
    /// assign exactly one material per section. Masked = two-sided middle (cutout).
    public sealed class WallSection
    {
        public string Texture { get; }
        public bool Masked { get; }
        public MeshData Mesh { get; }

        public WallSection(string texture, bool masked, MeshData mesh)
        {
            Texture = texture;
            Masked = masked;
            Mesh = mesh;
        }
    }
}
