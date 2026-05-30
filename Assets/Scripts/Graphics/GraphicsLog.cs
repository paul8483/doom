using System;

namespace Doom.Graphics
{
    public static class GraphicsLog
    {
        public static event Action<string> WarningHandler;
        public static event Action<string> ErrorHandler;

        public static void Warning(string msg) => WarningHandler?.Invoke(msg);
        public static void Error(string msg) => ErrorHandler?.Invoke(msg);
    }
}
