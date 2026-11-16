namespace Fish.Configuration;

public readonly record struct BobberTuning(int MinimumRating, int ColorTolerance);
public readonly record struct SplashTuning(int PixelTolerance, int MinimumChangePercent);

/// <summary>Maps the two user controls to fixed, predictable detection curves.</summary>
public static class DetectionSensitivity
{
    public static BobberTuning Bobber(BotOptions options)
    {
        return new(Map(options.BobberSensitivity, FishingDefaults.BobberMinimumRating, 95, 1),
            Map(options.BobberSensitivity, FishingDefaults.BobberColorTolerance, 0, 90));
    }

    public static SplashTuning Splash(BotOptions options)
    {
        return new(Map(options.SplashSensitivity, FishingDefaults.SplashPixelTolerance, 100, 10),
            Map(options.SplashSensitivity, FishingDefaults.SplashChangePercent, 25, 1));
    }

    internal static int Map(int sensitivity, int reference, int strict, int sensitive)
    {
        int level = Math.Clamp(sensitivity, 0, 100);
        double distance = Math.Abs(level - 50) / 50.0;
        // Flat near the reference for fine adjustment, smooth at the middle and at both ends.
        double weight = distance * distance * (3 - 2 * distance);
        return (int)Math.Round(reference + ((level < 50 ? strict : sensitive) - reference) * weight,
            MidpointRounding.AwayFromZero);
    }
}
