using Fish.Configuration;
using Fish.Core;
using System.Text.Json;

static class SettingsTests
{
    public static void Run(Action<bool, string> check, string folder)
    {
        var store = new SettingsStore(folder);
        var defaults = store.Load();
        check(defaults is { BobberAreaWidth: 600, BobberAreaHeight: 300, BobberAreaXOffset: 0, BobberAreaYOffset: 0 },
            "Neuer Suchbereich startet bei 600 × 300 ohne Versatz");
        check(defaults is { FishingHotkey: "1", BobberSensitivity: 50, SplashSensitivity: 50 }, "Angeltaste und Toleranzregler behalten ihre Defaults");
        check(defaults is { BaitEnabled: false, BaitHotkey: "2", BaitFiveMinutes: false },
            "Köder ist standardmäßig aus, auf Taste 2 belegt und auf zehn Minuten eingestellt");
        var runtime = FishingOptions.From(defaults);
        check(runtime.BaitDuration == TimeSpan.FromMinutes(10), "Die Köderwirkzeit beträgt standardmäßig zehn Minuten");
        check(runtime is { CastTimeoutSeconds: 25, BobberTimeoutSeconds: 5, LootDelayMilliseconds: 1000, ScanIntervalMilliseconds: 100 },
            "Feste Angelzeiten stammen ausschließlich aus dem Programm");
        check(runtime.SplashRegion == new SearchRegion(50, 50, 0, 0) && runtime.Motion is { Adaptive: true, CalibrationMilliseconds: 1000 },
            "Biss-Geometrie, Kalibrierung und Wasseranpassung bleiben fest");
        store.Save(new BotOptions { BobberSensitivity = 400, SplashSensitivity = -1, BobberAreaWidth = 1, BobberAreaHeight = 9999 });
        check(store.Load() is { BobberSensitivity: 100, SplashSensitivity: 0, BobberAreaWidth: 100, BobberAreaHeight: 2160 },
            "Nur veränderbare Werte werden innerhalb ihrer Grenzen normalisiert");
        store.Save(new BotOptions { FishingHotkey = "Strg+F8", BaitEnabled = true, BaitHotkey = "Umschalt+F9", BaitFiveMinutes = true,
            BobberSensitivity = 72, SplashSensitivity = 81,
            BobberAreaWidth = 720, BobberAreaHeight = 360, BobberAreaXOffset = 24, BobberAreaYOffset = -16,
            WindowXPosition = 120, WindowYPosition = 90 });
        var restarted = new SettingsStore(folder).Load();
        check(restarted is { BobberAreaWidth: 720, BobberAreaHeight: 360, BobberAreaXOffset: 24, BobberAreaYOffset: -16,
            WindowXPosition: 120, WindowYPosition: 90, FishingHotkey: "Ctrl+F8", BobberSensitivity: 72, SplashSensitivity: 81 },
            "Angeltaste, Regler, Mausgeometrie und Fensterposition überstehen den Neustart");
        var bait = FishingOptions.From(restarted);
        check(restarted is { BaitEnabled: true, BaitHotkey: "Shift+F9", BaitFiveMinutes: true } &&
            bait is { BaitEnabled: true, BaitHotkey: { Key: 0x78, Modifiers: HotkeyModifiers.Shift } } &&
            bait.BaitDuration == TimeSpan.FromMinutes(5),
            "Köderoption, Hotkey und die aktivierte Fünf-Minuten-Wirkzeit überstehen den Neustart");
        string path = Path.Combine(folder, SettingsStore.FileName);
        using (var json = JsonDocument.Parse(File.ReadAllText(path)))
        {
            string[] expected = ["SchemaVersion", "FishingHotkey", "BaitEnabled", "BaitHotkey", "BaitFiveMinutes", "WindowXPosition", "WindowYPosition", "BobberAreaWidth", "BobberAreaHeight",
                "BobberAreaXOffset", "BobberAreaYOffset", "BobberSensitivity", "SplashSensitivity"];
            check(json.RootElement.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(expected.Order()),
                "JSON enthält ausschließlich Versionsnummer und tatsächlich veränderbare Werte");
        }
        check(File.Exists(path + ".bak"), "Speichern erhält die vorherige gültige Konfiguration als Sicherung");
        File.WriteAllText(path, """
            {"SchemaVersion":2,"Theme":"Dark","AlwaysOnTop":false,"CastTimeoutSeconds":8,"BobberTimeoutSeconds":1,
             "SplashAreaWidth":900,"SplashAdaptiveThreshold":false,"BobberAreaWidth":640,"BobberAreaHeight":320,
             "WindowWidth":1200,"WindowHeight":900,"WindowMaximized":true,"WindowDpi":144,"UnknownSetting":true}
            """);
        var legacy = store.Load(); store.Save(legacy);
        check(legacy is { SchemaVersion: 7, BobberAreaWidth: 640, BobberAreaHeight: 320 } &&
            FishingOptions.From(legacy) is { CastTimeoutSeconds: 25, BobberTimeoutSeconds: 5 },
            "Alte Konfiguration übernimmt nur veränderbare Werte; feste Optionen bleiben unverändert");
        string saved = File.ReadAllText(path);
        check(new[] { "Theme", "AlwaysOnTop", "Tray", "TimeoutSeconds", "WindowWidth", "WindowHeight", "WindowMaximized", "WindowDpi",
            "BobberMatchMinimum", "BobberColorTolerance", "SplashPixelTolerance", "SplashChangePercent", "UnknownSetting" }.All(name => !saved.Contains(name)),
            "Entfernte Fensteroptionen und alte Einzelregler werden nicht mehr geschrieben");
        File.WriteAllText(path, """{"SchemaVersion":1,"UnknownSetting":255,}""");
        check(store.Load() is { BobberAreaWidth: 600, BobberAreaHeight: 300, BobberSensitivity: 50, SplashSensitivity: 50 },
            "Fehlende Werte erhalten Defaults; unbekannte Werte und abschließende Kommas bleiben kompatibel");
        File.WriteAllText(path, """{"SchemaVersion":7,"BaitHotkey":"Alt+Tab"}""");
        check(store.Load() is { BaitEnabled: false, BaitHotkey: "2", BaitFiveMinutes: false },
            "Eine ungültige Ködertaste fällt auf Taste 2 zurück, bei fehlender Wirkzeit gelten zehn Minuten");
        check(new SettingsStore().Folder == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fish"),
            "Standard-Speicherort ist benutzerbezogenes LocalAppData/Fish");
        File.WriteAllText(path, """{"SchemaVersion":999}""");
        bool rejected = false;
        try { store.Load(); } catch (NotSupportedException) { rejected = true; }
        check(rejected && File.ReadAllText(path).Contains("999"), "Neuere Formate werden auch bei vorhandener Sicherung nicht zurückgestuft");
        SettingsRecoveryTests.Run(check, Path.Combine(folder, "storage"));
    }
}
