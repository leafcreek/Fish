namespace Fish.Core;

public sealed record SplashMeasurement(double ChangedPercent, double LargestComponentPercent, double BaselinePercent,
    double ThresholdPercent, bool Ready, bool Triggered, bool NewTrigger, bool SceneChanged)
{
    public string Status => Triggered ? "Ausgelöst · warte auf Loot" : SceneChanged ? "Bildwechsel · neu kalibrieren" :
        Ready ? "Bewegung beobachten" : "Kalibrierung";
    public string Summary => $"Änderung {ChangedPercent:F1} % · Grenze {ThresholdPercent:F1} %\nNormal {BaselinePercent:F1} % · {Status}";
}

public sealed record SplashAnalysis(PixelFrame Preview, SplashMeasurement Measurement);

/// <summary>Consecutive raw-frame differences; one trigger until Reset starts another cast/observation.</summary>
public sealed class SplashMotionDetector(int pixelTolerance, int minimumPercent, bool adaptive, int calibrationMilliseconds)
{
    private int[]? _previous;
    private byte[] _changed = [];
    private int[] _queue = [];
    private int _width, _height, _comparisons;
    private DateTimeOffset _started;
    private double _baseline;
    private bool _triggered;

    public void Reset()
    {
        _previous = null; _comparisons = 0; _baseline = 0; _triggered = false;
    }

    public SplashAnalysis Analyze(PixelFrame frame, DateTimeOffset now)
    {
        if (pixelTolerance is < 1 or > 255 || minimumPercent is < 1 or > 80 || calibrationMilliseconds is < 0 or > 3000)
            throw new ArgumentOutOfRangeException(nameof(pixelTolerance), "Ungültige Einstellungen für den Bildvergleich.");
        if (frame.Width < 1 || frame.Height < 1 || frame.Pixels.Length != checked(frame.Width * frame.Height))
            throw new ArgumentException("Ungültiger Splash-Bildpuffer.");
        if (_previous == null || frame.Width != _width || frame.Height != _height)
        {
            Initialize(frame, now);
            return new(frame, Measure(0, 0, false, false, false));
        }

        Array.Clear(_changed);
        int changedCount = 0;
        for (int i = 0; i < frame.Pixels.Length; i++)
        {
            int before = _previous[i], after = frame.Pixels[i];
            int difference = Math.Max(Math.Abs(((after >> 16) & 255) - ((before >> 16) & 255)),
                Math.Max(Math.Abs(((after >> 8) & 255) - ((before >> 8) & 255)), Math.Abs((after & 255) - (before & 255))));
            if (difference >= pixelTolerance) { _changed[i] = 1; changedCount++; }
        }
        double changedPercent = 100.0 * changedCount / frame.Pixels.Length;
        int largest = LargestComponent();
        double largestPercent = 100.0 * largest / frame.Pixels.Length;
        var marked = Mark(frame);

        // Camera/lighting/occlusion changes affecting almost the entire crop need a new reference, not a click.
        if (changedPercent >= 85)
        {
            Initialize(frame, now); // Preserve a trigger already issued in this cast.
            return new(marked, Measure(changedPercent, largestPercent, false, false, true));
        }

        _comparisons++;
        bool ready = _comparisons >= 3 && (now - _started).TotalMilliseconds >= calibrationMilliseconds;
        double threshold = Threshold();
        double baseline = _baseline;
        int minimumComponent = Math.Max(4, (int)Math.Ceiling(frame.Pixels.Length * .01));
        bool hit = ready && !_triggered && changedPercent >= threshold && largest >= minimumComponent;
        if (hit) _triggered = true;
        // Learn ordinary motion only; avoid teaching the baseline a bite or a large outlier.
        if (!_triggered && (!ready || changedPercent < threshold))
            _baseline = _comparisons == 1 ? changedPercent : _baseline + .15 * (changedPercent - _baseline);
        Array.Copy(frame.Pixels, _previous, frame.Pixels.Length);
        return new(marked, new(changedPercent, largestPercent, baseline, threshold, ready, _triggered, hit, false));
    }

    private double Threshold() => adaptive ? Math.Min(80, Math.Max(minimumPercent, _baseline * 2.5 + 2)) : minimumPercent;

    private SplashMeasurement Measure(double change, double largest, bool ready, bool hit, bool sceneChanged)
        => new(change, largest, _baseline, Threshold(), ready, _triggered, hit, sceneChanged);

    private void Initialize(PixelFrame frame, DateTimeOffset now)
    {
        _width = frame.Width; _height = frame.Height; _started = now; _comparisons = 0; _baseline = 0;
        if (_previous?.Length != frame.Pixels.Length)
        {
            _previous = new int[frame.Pixels.Length];
            _changed = new byte[frame.Pixels.Length];
            _queue = new int[frame.Pixels.Length];
        }
        Array.Copy(frame.Pixels, _previous, frame.Pixels.Length);
    }

    private int LargestComponent()
    {
        int largest = 0;
        for (int start = 0; start < _changed.Length; start++)
        {
            if (_changed[start] != 1) continue;
            int head = 0, tail = 1;
            _queue[0] = start; _changed[start] = 2;
            while (head < tail)
            {
                int index = _queue[head++], x = index % _width, y = index / _width;
                for (int ny = Math.Max(0, y - 1); ny <= Math.Min(_height - 1, y + 1); ny++)
                for (int nx = Math.Max(0, x - 1); nx <= Math.Min(_width - 1, x + 1); nx++)
                {
                    int neighbour = ny * _width + nx;
                    if (_changed[neighbour] != 1) continue;
                    _changed[neighbour] = 2; _queue[tail++] = neighbour;
                }
            }
            largest = Math.Max(largest, tail);
        }
        return largest;
    }

    private PixelFrame Mark(PixelFrame frame)
    {
        var pixels = (int[])frame.Pixels.Clone();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (_changed[i] == 0) continue;
            int p = pixels[i];
            pixels[i] = Math.Max(180, (p >> 16) & 255) << 16 | (((p >> 8) & 255) / 3) << 8 | (p & 255) / 3;
        }
        return new(frame.Width, frame.Height, pixels);
    }
}
