using Fish.Core;

static class BobberTestImages
{
    public static PixelFrame BlueAndBody() => OnlyFeatures(BobberFeatures.Blue | BobberFeatures.Body);

    public static PixelFrame OnlyFeatures(BobberFeatures keep)
    {
        var source = ReferenceBobber.Default.Image;
        var profile = BobberColorProfile.Default;
        var pixels = (int[])source.Pixels.Clone();
        for (int i = 0; i < pixels.Length; i++)
        {
            var color = HsvColor.FromRgb(pixels[i]);
            double blue = profile.Blue.Similarity(color), red = profile.Red.Similarity(color), body = profile.Body.Similarity(color);
            double best = Math.Max(blue, Math.Max(red, body));
            var feature = profile.Metal.Similarity(color) > 0 ? BobberFeatures.Metal : best == 0 ? BobberFeatures.None
                : best == blue ? BobberFeatures.Blue : best == red ? BobberFeatures.Red : BobberFeatures.Body;
            if (feature == BobberFeatures.None || !keep.HasFlag(feature)) pixels[i] = 0;
        }
        return new(source.Width, source.Height, pixels);
    }
    public static PixelFrame Water(int width, int height)
    {
        var pixels = new int[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int wave = (int)(6 * Math.Sin(x * .07 + y * .4) + 3 * Math.Sin(y * .1));
            pixels[y * width + x] = (64 + wave) << 16 | (60 + wave) << 8 | (61 + wave);
        }
        return new(width, height, pixels);
    }

    public static void Paste(PixelFrame destination, PixelFrame source, int x, int y)
    {
        for (int row = 0; row < source.Height; row++)
        for (int column = 0; column < source.Width; column++)
        {
            int pixel = source.Pixels[row * source.Width + column];
            int alpha = (int)((uint)pixel >> 24);
            int index = (row + y) * destination.Width + x + column;
            int background = destination.Pixels[index], blended = 0;
            for (int shift = 0; shift <= 16; shift += 8)
                blended |= ((((pixel >> shift) & 255) * alpha + ((background >> shift) & 255) * (255 - alpha) + 127) / 255) << shift;
            destination.Pixels[index] = blended;
        }
    }

    // Independent forward renderer: supersample source pixels and deposit their projected areas in the scene.
    // Production detection compares color components and their relative layout, not the source image.
    internal static void WarpOnto(PixelFrame scene, ReferenceBobber template, PixelPoint target,
        double scale, double angle, double aspect, double perspective)
    {
        double cx = (template.Image.Width - 1) / 2.0, cy = (template.Image.Height - 1) / 2.0;
        double radians = angle * Math.PI / 180;
        (double X, double Y) Project(double x, double y)
        {
            double depth = 1 + perspective * (y - cy) / cy;
            double u = (x - cx) * scale * aspect / depth, v = (y - cy) * scale / depth;
            return (u * Math.Cos(radians) - v * Math.Sin(radians), u * Math.Sin(radians) + v * Math.Cos(radians));
        }
        var anchor = Project(template.Anchor.X, template.Anchor.Y);
        var coverage = new double[scene.Pixels.Length];
        var colors = new double[scene.Pixels.Length * 3];
        for (int y = 0; y < template.Image.Height; y++)
        for (int x = 0; x < template.Image.Width; x++)
        {
            int pixel = template.Image.Pixels[y * template.Image.Width + x];
            double alpha = ((uint)pixel >> 24) / 255.0;
            if (alpha == 0) continue;
            for (int sy = 0; sy < 4; sy++)
            for (int sx = 0; sx < 4; sx++)
            {
                double sourceX = x + (sx + .5) / 4 - .5, sourceY = y + (sy + .5) / 4 - .5;
                var point = Project(sourceX, sourceY);
                int dx = (int)Math.Round(target.X + point.X - anchor.X), dy = (int)Math.Round(target.Y + point.Y - anchor.Y);
                if (dx < 0 || dy < 0 || dx >= scene.Width || dy >= scene.Height) continue;
                double depth = 1 + perspective * (sourceY - cy) / cy;
                double weight = alpha * scale * scale * aspect / (16 * depth * depth * depth);
                int index = dy * scene.Width + dx;
                coverage[index] += weight;
                for (int channel = 0; channel < 3; channel++) colors[index * 3 + channel] += weight * ((pixel >> (8 * channel)) & 255);
            }
        }
        for (int i = 0; i < scene.Pixels.Length; i++)
        {
            if (coverage[i] == 0) continue;
            double alpha = Math.Min(1, coverage[i]);
            int blended = 0;
            for (int channel = 0; channel < 3; channel++) blended |= (int)Math.Round(
                colors[i * 3 + channel] / coverage[i] * alpha + ((scene.Pixels[i] >> (8 * channel)) & 255) * (1 - alpha)) << (8 * channel);
            scene.Pixels[i] = blended;
        }
    }

}
