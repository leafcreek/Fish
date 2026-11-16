using Fish.Core;
namespace Fish.Tests.Support;

/// <summary>The user's transparent PNG, with an anchor in the bobber body.</summary>
internal sealed record ReferenceBobber(PixelFrame Image, PixelPoint Anchor)
{
    private static readonly Lazy<ReferenceBobber> Bundled = new(LoadBundled);
    public static ReferenceBobber Default => Bundled.Value;

    private static ReferenceBobber LoadBundled()
    {
        using var stream = typeof(ReferenceBobber).Assembly.GetManifestResourceStream("Fish.Tests.bobber-reference.png")
            ?? throw new InvalidDataException("Die Schwimmer-Bildvorlage fehlt.");
        return FromImage(Prepare(TestBitmapDecoder.Read(stream)));
    }

    public static ReferenceBobber FromImage(PixelFrame image)
    {
        if (image.Width < 2 || image.Height < 2 || image.Pixels.Length != checked(image.Width * image.Height))
            throw new ArgumentException("Ungültige Schwimmer-Bildvorlage.");
        // Stay below the feather attachment. A wide feather must not pull the loot anchor away from the body.
        double weight = 0, xSum = 0, ySum = 0;
        for (int y = (int)(image.Height * .55); y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            double alpha = (uint)image.Pixels[y * image.Width + x] >> 24;
            weight += alpha; xSum += x * alpha; ySum += y * alpha;
        }
        if (weight == 0) throw new ArgumentException("Der Schwimmerkörper ist vollständig transparent.");
        return new(image, new((int)Math.Round(xSum / weight), (int)Math.Round(ySum / weight)));
    }

    /// <summary>Prepare a transparent fixture for reference measurement without changing its silhouette or colors.</summary>
    internal static PixelFrame Prepare(PixelFrame source)
    {
        if (source.Width < 2 || source.Height < 2 || source.Pixels.Length != checked(source.Width * source.Height))
            throw new ArgumentException("Ungültige Schwimmer-Bildvorlage.");
        int left = source.Width, top = source.Height, right = -1, bottom = -1;
        for (int y = 0; y < source.Height; y++)
        for (int x = 0; x < source.Width; x++)
        {
            if ((uint)source.Pixels[y * source.Width + x] >> 24 == 0) continue;
            left = Math.Min(left, x); top = Math.Min(top, y);
            right = Math.Max(right, x); bottom = Math.Max(bottom, y);
        }
        if (right < left) throw new ArgumentException("Die Bildvorlage ist vollständig transparent.");
        int croppedWidth = right - left + 1, croppedHeight = bottom - top + 1;
        double scale = Math.Min(1, Math.Min(144.0 / croppedWidth, 88.0 / croppedHeight));
        int width = Math.Max(2, (int)Math.Round(croppedWidth * scale));
        int height = Math.Max(2, (int)Math.Round(croppedHeight * scale));
        var pixels = new int[checked(width * height)];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double x0 = x * croppedWidth / (double)width, x1 = (x + 1) * croppedWidth / (double)width;
            double y0 = y * croppedHeight / (double)height, y1 = (y + 1) * croppedHeight / (double)height;
            double alpha = 0, red = 0, green = 0, blue = 0;
            for (int sy = (int)y0; sy < Math.Ceiling(y1); sy++)
            for (int sx = (int)x0; sx < Math.Ceiling(x1); sx++)
            {
                double coverage = (Math.Min(x1, sx + 1) - Math.Max(x0, sx)) * (Math.Min(y1, sy + 1) - Math.Max(y0, sy));
                int pixel = source.Pixels[(top + sy) * source.Width + left + sx];
                double weight = coverage * ((uint)pixel >> 24);
                alpha += weight;
                red += weight * ((pixel >> 16) & 255); green += weight * ((pixel >> 8) & 255); blue += weight * (pixel & 255);
            }
            if (alpha == 0) continue;
            int opacity = Math.Clamp((int)Math.Round(alpha / ((x1 - x0) * (y1 - y0))), 0, 255);
            if (opacity == 0) continue;
            pixels[y * width + x] = opacity << 24 | (int)Math.Round(red / alpha) << 16 |
                (int)Math.Round(green / alpha) << 8 | (int)Math.Round(blue / alpha);
        }
        return new(width, height, pixels);
    }
}
