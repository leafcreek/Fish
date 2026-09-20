namespace Fish.Core;

// The capture buffer belongs to the engine. Retain only small, independent copies.
public sealed class PreviewBuffer
{
    private readonly object _gate = new();
    private PixelFrame? _live, _bobber, _splash;
    private long _lastLive;

    public void Add(PixelFrame frame, bool splash, bool found)
    {
        lock (_gate)
        {
            long now = Environment.TickCount64;
            bool live = now - _lastLive >= 200;
            if (!live && !found) return;
            var copy = Copy(frame);
            if (live) { _live = copy; _lastLive = now; }
            if (found) { if (splash) _splash = copy; else _bobber = copy; }
        }
    }

    public (PixelFrame? Live, PixelFrame? Bobber, PixelFrame? Splash) Take()
    {
        lock (_gate)
        {
            var result = (_live, _bobber, _splash);
            _live = _bobber = _splash = null;
            return result;
        }
    }

    private static PixelFrame Copy(PixelFrame frame)
    {
        double scale = Math.Min(1, 480.0 / Math.Max(frame.Width, frame.Height));
        int width = Math.Max(1, (int)(frame.Width * scale)), height = Math.Max(1, (int)(frame.Height * scale));
        var pixels = new int[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            pixels[y * width + x] = frame.Pixels[(y * frame.Height / height) * frame.Width + x * frame.Width / width] | unchecked((int)0xff000000);
        return new(width, height, pixels);
    }
}
