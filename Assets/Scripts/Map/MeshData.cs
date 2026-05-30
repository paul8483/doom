namespace Doom.Map
{
    public readonly struct Float3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public Float3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    public readonly struct Float2
    {
        public readonly float X;
        public readonly float Y;
        public Float2(float x, float y) { X = x; Y = y; }
    }

    public sealed class MeshData
    {
        public Float3[] Vertices { get; }
        public int[] Triangles { get; }
        /// Texture coords, 1:1 with Vertices. Empty when the mesh is untextured.
        public Float2[] Uv { get; }
        /// Per-vertex RGB in 0..1 (sector light). Empty when unlit.
        public Float3[] Colors { get; }

        public MeshData(Float3[] vertices, int[] triangles)
            : this(vertices, triangles,
                   System.Array.Empty<Float2>(), System.Array.Empty<Float3>())
        { }

        public MeshData(Float3[] vertices, int[] triangles, Float2[] uv, Float3[] colors)
        {
            Vertices = vertices;
            Triangles = triangles;
            Uv = uv ?? System.Array.Empty<Float2>();
            Colors = colors ?? System.Array.Empty<Float3>();
        }

        public bool IsEmpty => Vertices.Length == 0 || Triangles.Length == 0;
        public static MeshData Empty { get; } =
            new MeshData(System.Array.Empty<Float3>(), System.Array.Empty<int>());
    }
}
