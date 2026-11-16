namespace Fish.Configuration;

/// <summary>Fixed fishing behavior; only detection thresholds and the draggable bobber region are configurable.</summary>
public static class FishingDefaults
{
    public const int CastTimeoutSeconds = 25;
    public const int BobberTimeoutSeconds = 5;
    public const int CastSettlingMilliseconds = 2000;
    public const int LootDelayMilliseconds = 1000;
    public const int ScanIntervalMilliseconds = 100;
    public const int SplashCalibrationMilliseconds = 1000;
    public const int BobberMinimumRating = 50, BobberColorTolerance = 50;
    public const int SplashPixelTolerance = 50, SplashChangePercent = 10;
    public static SearchRegion SplashRegion => new(50, 50, 0, 0);
}
