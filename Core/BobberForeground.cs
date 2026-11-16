namespace Fish.Core;

/// <summary>Separates compact objects from the water using a moving, per-row color median.</summary>
internal static class BobberForeground
{
    public static void Mark(PixelFrame frame, byte[] foreground, CancellationToken cancellation, bool chromaticOnly = false)
    {
        // The window normally exceeds a feather's width, so horizontal water samples
        // contribute on both sides instead of making the feather its own background.
        const int radius = 96;
        Span<int> red = stackalloc int[32], green = stackalloc int[32], blue = stackalloc int[32];
        for (int y = 0; y < frame.Height; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            red.Clear(); green.Clear(); blue.Clear();
            int row = y * frame.Width, right = Math.Min(frame.Width - 1, radius), left = 0;
            for (int x = left; x <= right; x++) Add(frame.Pixels[row + x], red, green, blue, 1);
            for (int x = 0; x < frame.Width; x++)
            {
                int middle = (right - left + 2) / 2;
                int r = Median(red, middle), g = Median(green, middle), b = Median(blue, middle);
                int pixel = frame.Pixels[row + x];
                int pr = (pixel >> 16) & 255, pg = (pixel >> 8) & 255, pb = pixel & 255;
                int distance = chromaticOnly
                    ? Math.Max(Math.Abs((pr - pg) - (r - g)), Math.Abs((pb - pg) - (b - g)))
                    : Math.Max(Math.Abs(pr - r), Math.Max(Math.Abs(pg - g), Math.Abs(pb - b)));
                // Relative threshold also works when the water itself is very bright.
                int threshold = chromaticOnly ? 7 + Math.Max(r, Math.Max(g, b)) / 24 : 7 + Math.Max(r, Math.Max(g, b)) / 12;
                foreground[row + x] = distance >= threshold ? (byte)1 : (byte)0;
                int nextLeft = Math.Max(0, x + 1 - radius), nextRight = Math.Min(frame.Width - 1, x + 1 + radius);
                if (nextLeft > left) Add(frame.Pixels[row + left++], red, green, blue, -1);
                if (nextRight > right) Add(frame.Pixels[row + ++right], red, green, blue, 1);
            }
        }
    }

    private static void Add(int pixel, Span<int> r, Span<int> g, Span<int> b, int delta)
    { r[(pixel >> 19) & 31] += delta; g[(pixel >> 11) & 31] += delta; b[(pixel >> 3) & 31] += delta; }

    private static int Median(ReadOnlySpan<int> histogram, int middle)
    {
        int sum = 0;
        for (int i = 0; i < histogram.Length; i++) if ((sum += histogram[i]) >= middle) return i * 8 + 4;
        return 252;
    }

    public static void SuppressRipples(PixelFrame frame, byte[] source, byte[] destination, CancellationToken cancellation)
    {
        for (int y = 0; y < frame.Height; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x < frame.Width; x++)
            {
                int index = y * frame.Width + x;
                if (source[index] == 0) { destination[index] = 0; continue; }
                int rows = 0, total = 0;
                for (int ny = Math.Max(0, y - 4); ny <= Math.Min(frame.Height - 1, y + 4); ny++)
                {
                    total++;
                    for (int nx = Math.Max(0, x - 1); nx <= Math.Min(frame.Width - 1, x + 1); nx++)
                        if (source[ny * frame.Width + nx] != 0) { rows++; break; }
                }
                destination[index] = rows >= Math.Min(6, total) ? (byte)1 : (byte)0;
            }
        }
    }
}
