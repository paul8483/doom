namespace Doom.Map
{
    public readonly struct Float3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public Float3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    public sealed class MeshData
    {
        public Float3[] Vertices { get; }
        public int[] Triangles { get; }

        public MeshData(Float3[] vertices, int[] triangles)
        {
            Vertices = vertices;
            Triangles = triangles;
        }

        public bool IsEmpty => Vertices.Length == 0 || Triangles.Length == 0;
        public static MeshData Empty { get; } =
            new MeshData(System.Array.Empty<Float3>(), System.Array.Empty<int>());
    }
}
