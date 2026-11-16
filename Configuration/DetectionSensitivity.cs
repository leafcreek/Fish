namespace Fish.Configuration;

public readonly record struct BobberTuning(int MinimumRating, int ColorTolerance);
public readonly record struct SplashTuning(int PixelTolerance, int MinimumChangePercent);

/// <summary>Couples each pair around its saved reference at 50 without changing that reference.</summary>
public static class DetectionSensitivity
{
    public static BobberTuning Bobber(BotOptions options)
    {
        int rating = Math.Clamp(options.BobberMatchMinimum, 1, 99);
        int color = Math.Clamp(options.BobberColorTolerance, 0, 255);
        return new(Map(options.BobberSensitivity, rating, Math.Max(95, rating), 1),
            Map(options.BobberSensitivity, color, 0, Math.Max(90, color)));
    }

    public static SplashTuning Splash(BotOptions options)
    {
        int noise = Math.Clamp(options.SplashPixelTolerance, 1, 255);
        int change = Math.Clamp(options.SplashChangePercent, 1, 80);
        return new(Map(options.SplashSensitivity, noise, Math.Max(100, noise), Math.Min(10, noise)),
            Map(options.SplashSensitivity, change, Math.Max(25, change), 1));
    }

    private static int Map(int sensitivity, int reference, int strict, int sensitive)
    {
        int level = Math.Clamp(sensitivity, 0, 100);
        double distance = Math.Abs(level - 50) / 50.0;
        // Flat near the reference for fine adjustment, smooth at the middle and at both ends.
        double weight = distance * distance * (3 - 2 * distance);
        return (int)Math.Round(reference + ((level < 50 ? strict : sensitive) - reference) * weight,
            MidpointRounding.AwayFromZero);
    }
}
