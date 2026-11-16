namespace Fish.Core;

[Flags]
public enum BobberFeatures { None = 0, Blue = 1, Red = 2, Body = 4, Metal = 8, All = Blue | Red | Body | Metal }

public sealed record BobberMatch(PixelRect Bounds, PixelPoint Point, double Confidence, double Scale,
    BobberFeatures Features = BobberFeatures.Blue | BobberFeatures.Red | BobberFeatures.Body)
{
    public int FeatureCount => System.Numerics.BitOperations.PopCount((uint)(Features & BobberFeatures.All));
    public bool NeedsConfirmation => FeatureCount == 2;
    public string Colors => string.Join(" + ", new[] {
        (BobberFeatures.Blue, "Blau"), (BobberFeatures.Red, "Rot"),
        (BobberFeatures.Body, "Körper"), (BobberFeatures.Metal, "Metall") }
        .Where(feature => Features.HasFlag(feature.Item1)).Select(feature => feature.Item2));
}

internal static class BobberPreview
{
    public static PixelFrame Mark(PixelFrame frame, BobberMatch match, bool confirmed = true)
    {
        var pixels = (int[])frame.Pixels.Clone();
        var rect = match.Bounds;
        int color = confirmed ? 0x00ff80 : 0xffb640;
        for (int x = rect.X; x < rect.X + rect.Width; x++)
        {
            pixels[rect.Y * frame.Width + x] = color;
            pixels[(rect.Y + rect.Height - 1) * frame.Width + x] = color;
        }
        for (int y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            pixels[y * frame.Width + rect.X] = color;
            pixels[y * frame.Width + rect.X + rect.Width - 1] = color;
        }
        for (int offset = -4; offset <= 4; offset++)
        {
            int x = match.Point.X + offset, y = match.Point.Y + offset;
            if (x >= 0 && x < frame.Width) pixels[match.Point.Y * frame.Width + x] = color;
            if (y >= 0 && y < frame.Height) pixels[y * frame.Width + match.Point.X] = color;
        }
        return new(frame.Width, frame.Height, pixels);
    }

}
