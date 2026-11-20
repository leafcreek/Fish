using System.Text.Json.Serialization;

namespace Fish.Configuration;

public sealed class BotOptions
{
    public const int CurrentSchemaVersion = 7;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string FishingHotkey { get; set; } = "1";
    public bool BaitEnabled { get; set; }
    public bool BaitFiveMinutes { get; set; }
    public string BaitHotkey { get; set; } = "2";
    internal TimeSpan BaitDuration => TimeSpan.FromMinutes(BaitFiveMinutes ? 5 : 10);
    public const int MinimumWindowWidth = 400, MinimumWindowHeight = 360;
    public const int DefaultWindowWidth = 480, DefaultWindowHeight = 480;
    public int WindowXPosition { get; set; }
    public int WindowYPosition { get; set; }
    public int BobberAreaWidth { get; set; } = 600;
    public int BobberAreaHeight { get; set; } = 300;
    public int BobberAreaXOffset { get; set; }
    public int BobberAreaYOffset { get; set; }
    public int BobberSensitivity { get; set; } = 50;
    public int SplashSensitivity { get; set; } = 50;

    public BotOptions Copy() => (BotOptions)MemberwiseClone();

    public void Normalize()
    {
        FishingHotkey = Core.FishingHotkey.TryParse(FishingHotkey, out var hotkey) ? hotkey.ToString() : "1";
        BaitHotkey = Core.FishingHotkey.TryParse(BaitHotkey, out var baitHotkey) ? baitHotkey.ToString() : "2";
        BobberAreaWidth = Math.Clamp(BobberAreaWidth, 100, 3840);
        BobberAreaHeight = Math.Clamp(BobberAreaHeight, 100, 2160);
        BobberAreaXOffset = Math.Clamp(BobberAreaXOffset, -7680, 7680);
        BobberAreaYOffset = Math.Clamp(BobberAreaYOffset, -4320, 4320);
        BobberSensitivity = Math.Clamp(BobberSensitivity, 0, 100);
        SplashSensitivity = Math.Clamp(SplashSensitivity, 0, 100);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)]
[JsonSerializable(typeof(BotOptions))]
internal partial class SettingsJsonContext : JsonSerializerContext { }
