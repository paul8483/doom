namespace Doom.Graphics
{
    /// Lets Doom.Map ask for wall-texture dimensions (for UV) without referencing
    /// Unity. Implemented by TextureSet; stubbed in Doom.Map unit tests.
    public interface ITextureSizeSource
    {
        bool TryGetSize(string name, out int width, out int height);
    }
}
