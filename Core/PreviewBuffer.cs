namespace Fish.Core;

// The capture buffer belongs to the engine. Retain only small, independent copies.
public sealed class PreviewBuffer
{
    private readonly object _gate = new();
    private PixelFrame? _live, _bobber, _splash;
    private SplashMeasurement? _liveMotion, _splashMotion;
    private long _lastLive;

    public void Add(PixelFrame frame, bool splash, bool found, SplashMeasurement? motion = null)
    {
        lock (_gate)
        {
            long now = Environment.TickCount64;
            bool live = found || now - _lastLive >= 200;
            if (!live && !found) return;
            var copy = Copy(frame);
            if (live) { _live = copy; _liveMotion = motion; _lastLive = now; }
            if (found) { if (splash) { _splash = copy; _splashMotion = motion; } else _bobber = copy; }
        }
    }

    public (PixelFrame? Live, PixelFrame? Bobber, PixelFrame? Splash, SplashMeasurement? LiveMotion, SplashMeasurement? SplashMotion) Take()
    {
        lock (_gate)
        {
            var result = (_live, _bobber, _splash, _liveMotion, _splashMotion);
            _live = _bobber = _splash = null;
            _liveMotion = _splashMotion = null;
            return result;
        }
    }

    public void ClearLive()
    {
        lock (_gate) { _live = null; _liveMotion = null; }
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
