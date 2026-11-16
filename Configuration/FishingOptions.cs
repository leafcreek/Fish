using Fish.Core;

namespace Fish.Configuration;

public readonly record struct SearchRegion(int Width, int Height, int XOffset, int YOffset)
{
    public PixelRect Around(PixelPoint center, PixelRect bounds) => PixelRect.Around(center, Width, Height, XOffset, YOffset, bounds);
    public PixelRect In(PixelRect bounds) => Around(new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), bounds);
}

public sealed record MotionOptions(int PixelTolerance, int MinimumPercent)
{
    public bool Adaptive => true;
    public int CalibrationMilliseconds => FishingDefaults.SplashCalibrationMilliseconds;
    public SplashMotionDetector CreateDetector() => new(PixelTolerance, MinimumPercent, Adaptive, CalibrationMilliseconds);
}

/// <summary>Immutable runtime configuration. Window and application preferences stay on the UI thread.</summary>
public sealed record FishingOptions(FishingHotkey Hotkey, SearchRegion BobberRegion,
    int BobberMatchMinimum, int BobberColorTolerance, MotionOptions Motion)
{
    public int ScanIntervalMilliseconds => FishingDefaults.ScanIntervalMilliseconds;
    public int CastTimeoutSeconds => FishingDefaults.CastTimeoutSeconds;
    public int BobberTimeoutSeconds => FishingDefaults.BobberTimeoutSeconds;
    public int LootDelayMilliseconds => FishingDefaults.LootDelayMilliseconds;
    public SearchRegion SplashRegion => FishingDefaults.SplashRegion;
    public static FishingOptions From(BotOptions source)
    {
        var o = source.Copy(); o.Normalize();
        var bobber = DetectionSensitivity.Bobber(o);
        var splash = DetectionSensitivity.Splash(o);
        FishingHotkey.TryParse(o.FishingHotkey, out var hotkey);
        return new(hotkey, new(o.BobberAreaWidth, o.BobberAreaHeight, o.BobberAreaXOffset, o.BobberAreaYOffset),
            bobber.MinimumRating, bobber.ColorTolerance, new(splash.PixelTolerance, splash.MinimumChangePercent));
    }
}
