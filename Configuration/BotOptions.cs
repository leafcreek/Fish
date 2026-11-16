using System.Text.Json.Serialization;

namespace Fish.Configuration;

public sealed class BotOptions
{
    public bool WindowMaximized { get; set; }
    public int WindowDpi { get; set; }
    public const int CurrentSchemaVersion = 6;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string FishingHotkey { get; set; } = "1";
    public const int MinimumWindowWidth = 400, MinimumWindowHeight = 360;
    public const int DefaultWindowWidth = 480, DefaultWindowHeight = 400;
    public int WindowWidth { get; set; } = DefaultWindowWidth;
    public int WindowHeight { get; set; } = DefaultWindowHeight;
    public int WindowXPosition { get; set; }
    public int WindowYPosition { get; set; }
    public int BobberAreaWidth { get; set; } = 600;
    public int BobberAreaHeight { get; set; } = 300;
    public int BobberAreaXOffset { get; set; }
    public int BobberAreaYOffset { get; set; }
    public int BobberSensitivity { get; set; } = 50;
    public int SplashSensitivity { get; set; } = 50;
    // Saved reference pairs at sensitivity 50; moving a slider must not rewrite these anchors.
    public int BobberMatchMinimum { get; set; } = 50;
    public int BobberColorTolerance { get; set; } = 50;
    public int SplashPixelTolerance { get; set; } = 50;
    public int SplashChangePercent { get; set; } = 10;

    public BotOptions Copy() => (BotOptions)MemberwiseClone();

    public void Normalize()
    {
        FishingHotkey = Core.FishingHotkey.TryParse(FishingHotkey, out var hotkey) ? hotkey.ToString() : "1";
        WindowDpi = WindowDpi == 0 ? 0 : Math.Clamp(WindowDpi, 96, 768);
        WindowWidth = Math.Clamp(WindowWidth, MinimumWindowWidth, 7680);
        WindowHeight = Math.Clamp(WindowHeight, MinimumWindowHeight, 4320);
        BobberAreaWidth = Math.Clamp(BobberAreaWidth, 100, 3840);
        BobberAreaHeight = Math.Clamp(BobberAreaHeight, 100, 2160);
        BobberAreaXOffset = Math.Clamp(BobberAreaXOffset, -7680, 7680);
        BobberAreaYOffset = Math.Clamp(BobberAreaYOffset, -4320, 4320);
        BobberSensitivity = Math.Clamp(BobberSensitivity, 0, 100);
        SplashSensitivity = Math.Clamp(SplashSensitivity, 0, 100);
        BobberMatchMinimum = Math.Clamp(BobberMatchMinimum, 1, 99);
        BobberColorTolerance = Math.Clamp(BobberColorTolerance, 0, 255);
        SplashPixelTolerance = Math.Clamp(SplashPixelTolerance, 1, 255);
        SplashChangePercent = Math.Clamp(SplashChangePercent, 1, 80);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)]
[JsonSerializable(typeof(BotOptions))]
internal partial class SettingsJsonContext : JsonSerializerContext { }
