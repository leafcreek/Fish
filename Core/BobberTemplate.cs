namespace Fish.Core;

/// <summary>The user's transparent PNG, with an anchor in the bobber body.</summary>
public sealed record BobberTemplate(PixelFrame Image, PixelPoint Anchor, bool UseAlpha = false)
{
    private static readonly Lazy<BobberTemplate> Bundled = new(LoadBundled);
    public static BobberTemplate Default => Bundled.Value;

    private static BobberTemplate LoadBundled()
    {
        using var stream = typeof(BobberTemplate).Assembly.GetManifestResourceStream("Fish.Bobber.forever.png")
            ?? throw new InvalidDataException("Die Bobber-Bildvorlage fehlt.");
        return FromImage(Fish.Native.BitmapDecoder.Read(stream));
    }

    public static BobberTemplate FromImage(PixelFrame image)
    {
        if (image.Width < 2 || image.Height < 2 || image.Pixels.Length != checked(image.Width * image.Height))
            throw new ArgumentException("Ungültige Bobber-Bildvorlage.");
        // The lower 60 % contain the body. Derive its anchor from opacity so replacing the PNG needs no coordinates in code.
        double weight = 0, xSum = 0, ySum = 0;
        for (int y = (int)(image.Height * .4); y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            double alpha = (uint)image.Pixels[y * image.Width + x] >> 24;
            weight += alpha; xSum += x * alpha; ySum += y * alpha;
        }
        if (weight == 0) throw new ArgumentException("Der Schwimmerkörper ist vollständig transparent.");
        return new(image, new((int)Math.Round(xSum / weight), (int)Math.Round(ySum / weight)), UseAlpha: true);
    }
}
