namespace Fish.Core;

public enum BotState { None, WoWNotRunning, WoWNotActive, WANotFound, Idle, Fishing, Looting, Dead, FullBag }
public readonly record struct PixelPoint(int X, int Y);
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public PixelRect Intersect(PixelRect other)
    {
        int x = Math.Max(X, other.X), y = Math.Max(Y, other.Y);
        return new(x, y, Math.Max(0, Math.Min(X + Width, other.X + other.Width) - x),
            Math.Max(0, Math.Min(Y + Height, other.Y + other.Height) - y));
    }
}
public sealed record MonitorInfo(string Device, PixelRect Bounds, PixelRect? WorkArea = null, bool IsPrimary = false);
public sealed record PixelFrame(int Width, int Height, int[] Pixels);

public static class Detection
{
    public static BotState State(int argb) => (argb & 0xffffff) switch
    {
        0xff0000 => BotState.Idle, 0x0000ff => BotState.Fishing, 0x00ff00 => BotState.Looting,
        0xffffff => BotState.Idle,
        0x00ffff => BotState.FullBag, 0xffa500 => BotState.Dead,
        _ => BotState.WANotFound
    };

    public static PixelPoint? Find(PixelFrame frame, int skip, int tolerance, bool splash)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        if (frame.Pixels.Length != checked(frame.Width * frame.Height)) throw new ArgumentException("Ungültiger Bildpuffer.");
        tolerance = Math.Clamp(tolerance, 0, 255);
        for (int y = 0; y < frame.Height; y += skip + 1)
        for (int x = 0; x < frame.Width; x += skip + 1)
        {
            int p = frame.Pixels[y * frame.Width + x];
            int r = (p >> 16) & 255, g = (p >> 8) & 255, b = p & 255;
            bool found = splash
                ? Near(r, 250, tolerance) && Near(g, 250, tolerance) && Near(b, 250, tolerance)
                : r - g >= 255 - tolerance && r - b >= 255 - tolerance;
            if (found) return new(x, y);
        }
        return null;
    }

    private static bool Near(int value, int target, int tolerance) => Math.Abs(value - target) <= tolerance;

    public static PixelRect Around(PixelPoint center, int width, int height, int offsetX, int offsetY, PixelRect bounds)
        => new PixelRect(center.X - width / 2 + offsetX, center.Y - height / 2 - offsetY, width, height).Intersect(bounds);
}
