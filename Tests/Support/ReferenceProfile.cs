using Fish.Core;
using System.Security.Cryptography;

namespace Fish.Tests.Support;

internal static class ReferenceProfile
{
    private static string Fingerprint(PixelFrame image)
        => Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(image.Pixels.AsSpan())));

    public static BobberColorProfile Measure(ReferenceBobber template)
    {
        var image = template.Image;
        BobberColorSignature Measure(string name, double seedX, double seedY, double fromY, double toY, double tolerance, bool metallic = false)
        {
            int sx = Math.Clamp((int)(image.Width * seedX), 0, image.Width - 1), sy = Math.Clamp((int)(image.Height * seedY), 0, image.Height - 1);
            var seed = HsvColor.FromRgb(image.Pixels[sy * image.Width + sx]);
            var samples = new List<(int X, int Y, int Pixel)>();
            for (int y = (int)(image.Height * fromY); y < (int)(image.Height * toY); y++)
            for (int x = 0; x < image.Width; x++)
            {
                int pixel = image.Pixels[y * image.Width + x];
                if ((uint)pixel >> 24 < 220) continue;
                var color = HsvColor.FromRgb(pixel);
                if (metallic ? color.Saturation > .2 || color.Value < .42 || x < image.Width * .62
                    : color.Saturation < .1 || HsvColor.HueDistance(color.Hue, seed.Hue) > tolerance) continue;
                samples.Add((x, y, pixel));
            }
            if (samples.Count < 8) throw new InvalidDataException($"Die Schwimmer-Vorlage enthält zu wenig Farbe für {name}.");
            int Median(int shift) => samples.Select(s => (s.Pixel >> shift) & 255).Order().ElementAt(samples.Count / 2);
            int rgb = Median(16) << 16 | Median(8) << 8 | Median(0);
            var median = HsvColor.FromRgb(rgb);
            return new(name, $"#{rgb:X6}", median.Hue, median.Saturation, tolerance,
                Math.Max(.1, median.Saturation * .3),
                new((int)Math.Round(samples.Average(s => s.X)), (int)Math.Round(samples.Average(s => s.Y))),
                new(samples.Min(s => s.X), samples.Min(s => s.Y), samples.Max(s => s.X) - samples.Min(s => s.X) + 1,
                    samples.Max(s => s.Y) - samples.Min(s => s.Y) + 1), samples.Count, metallic);
        }
        var measured = new BobberColorProfile(Fingerprint(image), image.Width, image.Height, template.Anchor,
            Measure("Blaue Feder", .42, .18, 0, .52, 28), Measure("Rote Feder", .24, .35, 0, .55, 18),
            Measure("Schwimmerkörper", template.Anchor.X / (double)image.Width, template.Anchor.Y / (double)image.Height, .55, 1, 18),
            Measure("Metallöse", .75, .23, .13, .35, 0, metallic: true));
        // Layout and mass must describe the same connected components that the scan actually sees.
        // Otherwise a partial feather shifts an inferred body point, despite a correct color match.
        var regions = BobberSegmentation.Find(image, measured, default);
        BobberColorSignature Refine(BobberColorSignature signature, List<BobberRegion> groups)
        {
            var group = groups.Where(g => !g.Bounds.Intersect(signature.Bounds).IsEmpty).OrderByDescending(g => g.Count).FirstOrDefault();
            if (group.Count == 0) throw new InvalidDataException($"Keine zusammenhängende Referenz für {signature.Name}.");
            return signature with { Center = group.Center, Bounds = group.Bounds, Pixels = group.Count };
        }
        return measured with { Blue = Refine(measured.Blue, regions.Blue), Red = Refine(measured.Red, regions.Red),
            Body = Refine(measured.Body, regions.Bodies), Metal = Refine(measured.Metal, regions.Metal) };
    }
}
