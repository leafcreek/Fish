using System.Text.Json.Serialization;

namespace Fish.Configuration;

public sealed class BotOptions
{
    public string Theme { get; set; } = "Dark";
    public bool AlwaysOnTop { get; set; } = true;
    public bool WindowMaximized { get; set; }
    public int WindowDpi { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public int WindowWidth { get; set; } = 760;
    public int WindowHeight { get; set; } = 740;
    public int WindowXPosition { get; set; }
    public int WindowYPosition { get; set; }
    public int PerformanceScanRate { get; set; } = 150;
    public int BobberAreaWidth { get; set; } = 800;
    public int BobberAreaHeight { get; set; } = 400;
    public int BobberAreaXOffset { get; set; }
    public int BobberAreaYOffset { get; set; } = 30;
    public int BobberMatchMinimum { get; set; } = 80;
    public int SplashAreaWidth { get; set; } = 120;
    public int SplashAreaHeight { get; set; } = 100;
    public int SplashAreaXOffset { get; set; } = 30;
    public int SplashAreaYOffset { get; set; }
    public int SplashPixelTolerance { get; set; } = 20;
    public int SplashChangePercent { get; set; } = 5;
    public bool SplashAdaptiveThreshold { get; set; } = true;
    public int SplashCalibrationMilliseconds { get; set; } = 500;
    public bool FeaturesExitOnInactivity { get; set; }
    public bool FeaturesExitOnFullBag { get; set; }
    public bool FeaturesExitOnDeath { get; set; }
    public bool FeaturesShutdownOnInactivity { get; set; }
    public bool FeaturesShutdownOnFullBag { get; set; }
    public bool FeaturesShutdownOnDeath { get; set; }
    public int FeaturesCastTime { get; set; } = 25;

    public BotOptions Copy() => (BotOptions)MemberwiseClone();

    public void Normalize()
    {
        if (Theme is not ("Dark" or "Light")) Theme = "Dark";
        WindowDpi = WindowDpi == 0 ? 0 : Math.Clamp(WindowDpi, 96, 768);
        WindowWidth = Math.Clamp(WindowWidth, 720, 7680);
        WindowHeight = Math.Clamp(WindowHeight, 540, 4320);
        PerformanceScanRate = Math.Clamp(PerformanceScanRate, 100, 1000);
        BobberAreaWidth = Math.Clamp(BobberAreaWidth, 100, 3840);
        BobberAreaHeight = Math.Clamp(BobberAreaHeight, 100, 2160);
        SplashAreaWidth = Math.Clamp(SplashAreaWidth, 50, 1920);
        SplashAreaHeight = Math.Clamp(SplashAreaHeight, 50, 1080);
        BobberAreaXOffset = Math.Clamp(BobberAreaXOffset, -7680, 7680);
        BobberAreaYOffset = Math.Clamp(BobberAreaYOffset, -4320, 4320);
        SplashAreaXOffset = Math.Clamp(SplashAreaXOffset, -3840, 3840);
        SplashAreaYOffset = Math.Clamp(SplashAreaYOffset, -2160, 2160);
        BobberMatchMinimum = Math.Clamp(BobberMatchMinimum, 10, 99);
        SplashPixelTolerance = Math.Clamp(SplashPixelTolerance, 1, 255);
        SplashChangePercent = Math.Clamp(SplashChangePercent, 1, 80);
        SplashCalibrationMilliseconds = Math.Clamp(SplashCalibrationMilliseconds, 300, 3000);
        FeaturesCastTime = Math.Clamp(FeaturesCastTime, 5, 25);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)]
[JsonSerializable(typeof(BotOptions))]
internal partial class SettingsJsonContext : JsonSerializerContext { }
