using System.Text.Json;

namespace Fish.Core;

internal readonly record struct HsvColor(double Hue, double Saturation, double Value)
{
    public static HsvColor FromRgb(int pixel)
    {
        double r = ((pixel >> 16) & 255) / 255.0, g = ((pixel >> 8) & 255) / 255.0, b = (pixel & 255) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
        double hue = delta == 0 ? 0 : max == r ? 60 * ((g - b) / delta % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        return new(hue < 0 ? hue + 360 : hue, max == 0 ? 0 : delta / max, max);
    }
    public static double HueDistance(double a, double b) => Math.Min(Math.Abs(a - b), 360 - Math.Abs(a - b));
}

internal sealed record BobberColorSignature(string Name, string Hex, double Hue, double Saturation,
    double HueTolerance, double MinimumSaturation, PixelPoint Center, PixelRect Bounds, int Pixels, bool Metallic = false)
{
    public double Similarity(HsvColor color)
    {
        if (Metallic)
        {
            // Neutral silver, or the bright blue tint of metal under cool environmental light.
            if (color.Saturation <= .2 && color.Value >= .42) return 1 - color.Saturation * .5;
            return color.Hue is >= 170 and <= 220 && color.Saturation <= .4 && color.Value >= .42
                ? 1 - color.Saturation * .4 : 0;
        }
        if (color.Saturation < MinimumSaturation || color.Value < .035) return 0;
        double difference = HsvColor.HueDistance(color.Hue, Hue);
        if (difference > HueTolerance) return 0;
        // Brightness is deliberately absent: the same material may be in sunlight, shadow or fog.
        double relativeDifference = difference / HueTolerance;
        return Math.Max(0, 1 - .3 * relativeDifference * relativeDifference - .12 * Math.Abs(color.Saturation - Saturation));
    }
}

internal sealed record BobberColorProfile(string SourceFingerprint, int Width, int Height, PixelPoint Anchor,
    BobberColorSignature Blue, BobberColorSignature Red, BobberColorSignature Body, BobberColorSignature Metal)
{
    private static readonly Lazy<BobberColorProfile> Bundled = new(Load);
    public static BobberColorProfile Default => Bundled.Value;

    private static BobberColorProfile Load()
    {
        using var saved = typeof(BobberColorProfile).Assembly.GetManifestResourceStream("Fish.Bobber.colors.json")
            ?? throw new InvalidDataException("Das Schwimmer-Farbprofil fehlt.");
        return Read(saved);
    }

    internal static BobberColorProfile Read(Stream source)
    {
        var profile = JsonSerializer.Deserialize<BobberColorProfile>(source)
            ?? throw new InvalidDataException("Das Schwimmer-Farbprofil ist leer.");
        var frame = new PixelRect(0, 0, profile.Width, profile.Height);
        if (profile.Width is < 2 or > 4096 || profile.Height is < 2 or > 4096 || !frame.Contains(profile.Anchor))
            throw new InvalidDataException("Die Schwimmer-Referenzgeometrie ist ungültig.");
        if (profile.Blue is not { Metallic: false } || profile.Red is not { Metallic: false } ||
            profile.Body is not { Metallic: false } || profile.Metal is not { Metallic: true })
            throw new InvalidDataException("Die vier Schwimmer-Merkmale sind unvollständig oder falsch zugeordnet.");
        foreach (var color in new[] { profile.Blue, profile.Red, profile.Body, profile.Metal })
        {
            if (color == null || string.IsNullOrWhiteSpace(color.Name) || color.Hex is not { Length: 7 } || color.Hex[0] != '#' ||
                !uint.TryParse(color.Hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _) ||
                !frame.Contains(color.Bounds) || !color.Bounds.Contains(color.Center) || color.Pixels < 1 || color.Pixels > color.Bounds.Area ||
                !double.IsFinite(color.Hue) || color.Hue is < 0 or > 360 ||
                !double.IsFinite(color.Saturation) || color.Saturation is < 0 or > 1 ||
                !double.IsFinite(color.MinimumSaturation) || color.MinimumSaturation is < 0 or > 1 ||
                !double.IsFinite(color.HueTolerance) || color.HueTolerance < 0 || !color.Metallic && color.HueTolerance == 0)
                throw new InvalidDataException("Das Schwimmer-Farbprofil enthält ein ungültiges Merkmal.");
        }
        return profile;
    }
}

/// <summary>Adds a per-channel RGB acceptance radius; quality depends on the pixel, not the slider value.</summary>
internal readonly struct BobberColorMatcher
{
    private readonly BobberColorSignature _signature;
    private readonly int _rgb, _tolerance;

    public BobberColorMatcher(BobberColorSignature signature, int tolerance)
    {
        _signature = signature;
        _rgb = Convert.ToInt32(signature.Hex[1..], 16);
        _tolerance = tolerance;
    }

    public double Similarity(int pixel, HsvColor color)
    {
        double original = _signature.Similarity(color);
        if (original > 0 || _tolerance == 0) return original;
        int distance = Math.Max(Math.Abs(((pixel >> 16) & 255) - ((_rgb >> 16) & 255)),
            Math.Max(Math.Abs(((pixel >> 8) & 255) - ((_rgb >> 8) & 255)), Math.Abs((pixel & 255) - (_rgb & 255))));
        return distance <= _tolerance ? 1 - .3 * distance / 255.0 : 0;
    }
}
