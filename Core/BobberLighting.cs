namespace Fish.Core;

internal readonly record struct BobberLight(double Red, double Blue)
{
    public int Apply(int pixel) => Math.Clamp((int)Math.Round(((pixel >> 16) & 255) * Red), 0, 255) << 16 |
        (pixel & 0xff00) | Math.Clamp((int)Math.Round((pixel & 255) * Blue), 0, 255);
}

/// <summary>Proposes local white balances from bright, moderately saturated foreground highlights.</summary>
internal static class BobberLighting
{
    private struct Bin { public int Count; public double Weight, Red, Green, Blue; }

    public static IReadOnlyList<BobberLight> Estimate(PixelFrame frame, byte[] foreground, CancellationToken cancellation)
    {
        const int bins = 16;
        Span<Bin> colors = stackalloc Bin[bins * bins];
        colors.Clear();
        for (int i = 0; i < frame.Pixels.Length; i++)
        {
            if ((i & 2047) == 0) cancellation.ThrowIfCancellationRequested();
            if (foreground[i] == 0) continue;
            int p = frame.Pixels[i], r = (p >> 16) & 255, g = (p >> 8) & 255, b = p & 255;
            int maximum = Math.Max(r, Math.Max(g, b)), minimum = Math.Min(r, Math.Min(g, b));
            // Clipped highlights no longer preserve channel ratios and would bias the white point.
            if (g < 20 || maximum < 46 || maximum >= 245 || minimum < maximum * .45) continue;
            double red = r / (double)g, blue = b / (double)g;
            if (red is < .55 or > 1.8 || blue is < .55 or > 1.8 || Math.Abs(red - 1) < .08 && Math.Abs(blue - 1) < .08) continue;
            int index = Math.Clamp((int)((red - .5) * 10), 0, bins - 1) * bins + Math.Clamp((int)((blue - .5) * 10), 0, bins - 1);
            // Bright metal contributes much more than a larger dim cork or feather.
            double weight = Math.Pow(maximum / 255.0, 6);
            ref var bin = ref colors[index];
            bin.Count++; bin.Weight += weight; bin.Red += r * weight; bin.Green += g * weight; bin.Blue += b * weight;
        }
        var result = new List<BobberLight>(3);
        for (int candidate = 0; candidate < 3; candidate++)
        {
            int best = -1;
            for (int i = 0; i < colors.Length; i++)
                if (colors[i].Count >= 4 && colors[i].Weight > 0 && (best < 0 || colors[i].Weight > colors[best].Weight)) best = i;
            if (best < 0) break;
            var selected = colors[best]; colors[best] = default;
            var light = new BobberLight(selected.Green / selected.Red, selected.Green / selected.Blue);
            if (result.All(previous => Math.Abs(previous.Red - light.Red) + Math.Abs(previous.Blue - light.Blue) >= .15)) result.Add(light);
        }
        return result;
    }
}
