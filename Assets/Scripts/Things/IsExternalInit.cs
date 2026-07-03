// Shim: enables C# 9 init-only setters under netstandard2.1 (type is absent there).
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
