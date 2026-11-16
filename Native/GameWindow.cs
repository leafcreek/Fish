using Fish.Core;

namespace Fish.Native;

internal static class GameWindow
{
    internal sealed class Access : IGameWindow
    {
        public nint FindGame() => Find();
        public bool IsAvailable(nint game) => GameWindow.IsAvailable(game);
        public PixelRect? GetGameBounds(nint game) => Bounds(game);
    }
    public static nint Find() => Desktop.FindWindowW(null, "World of Warcraft");
    public static bool IsAvailable(nint game) => game != 0 && Desktop.IsWindow(game) && Desktop.IsWindowVisible(game) && !Desktop.IsIconic(game);
    public static PixelRect? Bounds(nint game)
    {
        if (game == 0 || !Desktop.IsWindow(game) || Desktop.IsIconic(game) || !Desktop.GetClientRect(game, out var rect)) return null;
        var origin = new Desktop.Point { X = rect.Left, Y = rect.Top };
        if (!Desktop.ClientToScreen(game, ref origin) || rect.Right <= rect.Left || rect.Bottom <= rect.Top) return null;
        return new(origin.X, origin.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
}
