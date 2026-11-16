using Fish.Configuration;

static class SettingsTests
{
    public static void Run(Action<bool, string> check, string folder)
    {
        var store = new SettingsStore(folder);
        var defaults = store.Load();
        check(defaults is { BobberAreaWidth: 600, BobberAreaHeight: 300, BobberAreaXOffset: 0, BobberAreaYOffset: 0 },
            "Neuer Bobber-Suchbereich startet bei 600 × 300 ohne Versatz");
        check(defaults is { WindowWidth: 480, WindowHeight: 400 }, "Kompakte Fenstergröße ist 480 × 400");
        var runtime = FishingOptions.From(defaults);
        check(runtime is { CastTimeoutSeconds: 25, BobberTimeoutSeconds: 5, LootDelayMilliseconds: 1000, ScanIntervalMilliseconds: 100 },
            "Feste Angelzeiten gelten unabhängig von gespeicherten Einstellungen");
        check(runtime.SplashRegion == new SearchRegion(50, 50, 0, 0) && runtime.Motion is { Adaptive: true, CalibrationMilliseconds: 1000 },
            "Splash-Geometrie, Kalibrierung und Wasseranpassung sind fest vorgegeben");
        check(defaults is { FishingHotkey: "1", BobberSensitivity: 50, SplashSensitivity: 50,
            BobberMatchMinimum: 50, BobberColorTolerance: 50, SplashPixelTolerance: 50, SplashChangePercent: 10 },
            "Verbleibende Hotkey- und Erkennungswerte behalten ihre Defaults");
        store.Save(new BotOptions { BobberMatchMinimum = 400, BobberColorTolerance = 999, SplashChangePercent = 0 });
        check(store.Load() is { BobberMatchMinimum: 99, BobberColorTolerance: 255, SplashChangePercent: 1 }, "Einstellbare Grenzwerte werden normalisiert");
        store.Save(new BotOptions { BobberMatchMinimum = 1, BobberColorTolerance = 0 });
        check(store.Load() is { BobberMatchMinimum: 1, BobberColorTolerance: 0 }, "Untere Grenzen bleiben beim Speichern und Laden erhalten");
        var low = new BotOptions { BobberMatchMinimum = 0, BobberColorTolerance = -1 }; low.Normalize();
        check(low is { BobberMatchMinimum: 1, BobberColorTolerance: 0 }, "Mindestbewertung und Farbtoleranz besitzen unabhängige Untergrenzen");
        store.Save(new BotOptions { BobberAreaWidth = 720, BobberAreaHeight = 360, BobberAreaXOffset = 24, BobberAreaYOffset = -16,
            WindowWidth = 1100, WindowHeight = 800, WindowXPosition = 120, WindowYPosition = 90, WindowMaximized = true, WindowDpi = 144,
            FishingHotkey = "Strg+F8", BobberMatchMinimum = 88, BobberColorTolerance = 60, SplashPixelTolerance = 24, SplashChangePercent = 7 });
        var restarted = new SettingsStore(folder).Load();
        check(restarted is { BobberAreaWidth: 720, BobberAreaHeight: 360, BobberAreaXOffset: 24, BobberAreaYOffset: -16 },
            "Über Mausgriffe gespeicherte Geometrie übersteht einen Neustart");
        check(restarted is { WindowWidth: 1100, WindowHeight: 800, WindowXPosition: 120, WindowYPosition: 90, WindowMaximized: true, WindowDpi: 144 },
            "Fensterposition und Größe werden weiter gespeichert");
        check(restarted is { FishingHotkey: "Ctrl+F8", BobberMatchMinimum: 88, BobberColorTolerance: 60, SplashPixelTolerance: 24, SplashChangePercent: 7 },
            "Hotkey und alle vier Erkennungsregler überstehen einen Neustart");
        check(File.Exists(Path.Combine(folder, "appsettings.json.bak")), "Atomisches Speichern erhält eine Sicherung");
        File.WriteAllText(Path.Combine(folder, "appsettings.json"), """
            {"SchemaVersion":2,"Theme":"Dark","AlwaysOnTop":false,"MinimizeToTray":true,"CloseToTray":true,
             "CastTimeoutSeconds":8,"BobberTimeoutSeconds":1,"LootDelayMilliseconds":300,"ScanIntervalMilliseconds":900,
             "SplashAreaWidth":900,"SplashAreaHeight":800,"SplashAreaXOffset":400,"SplashAreaYOffset":300,
             "SplashCalibrationMilliseconds":300,"SplashAdaptiveThreshold":false,
             "PerformanceScanRate":900,"FeaturesCastTime":8,"BobberAreaWidth":640,"BobberAreaHeight":320,
             "BobberMatchMinimum":42,"BobberColorTolerance":60,"WindowWidth":1200,"WindowHeight":900,
             "WindowMaximized":true,"WindowDpi":144,"UnknownSetting":true}
            """);
        var legacy = store.Load(); store.Save(legacy);
        runtime = FishingOptions.From(legacy);
        check(legacy is { SchemaVersion: 6, BobberAreaWidth: 640, BobberAreaHeight: 320, BobberMatchMinimum: 42, BobberColorTolerance: 60 } &&
            runtime is { CastTimeoutSeconds: 25, BobberTimeoutSeconds: 5, LootDelayMilliseconds: 1000, ScanIntervalMilliseconds: 100 } &&
            runtime.SplashRegion == new SearchRegion(50, 50, 0, 0) && runtime.Motion is { Adaptive: true, CalibrationMilliseconds: 1000 },
            "Alte Konfiguration übernimmt nur aktive Werte; entfernte Optionen können Festwerte nicht überschreiben");
        check(legacy is { WindowWidth: 480, WindowHeight: 400, WindowMaximized: false, WindowDpi: 96 },
            "Vorherige Fenstergröße wird beim Wechsel zur kompakten Oberfläche einmalig zurückgesetzt");
        var saved = File.ReadAllText(Path.Combine(folder, "appsettings.json"));
        check(new[] { "Theme", "AlwaysOnTop", "Tray", "TimeoutSeconds", "LootDelayMilliseconds", "ScanInterval", "SplashArea",
                "SplashCalibration", "SplashAdaptive", "PerformanceScanRate", "FeaturesCastTime", "UnknownSetting" }.All(name => !saved.Contains(name)),
            "Entfernte Einstellungen werden nicht mehr in JSON geschrieben");
        File.WriteAllText(Path.Combine(folder, "appsettings.json"), """{"SchemaVersion":1,"UnknownSetting":255,}""");
        check(store.Load() is { BobberAreaWidth: 600, BobberAreaHeight: 300, BobberMatchMinimum: 50, BobberColorTolerance: 50 }, "Fehlende Felder erhalten aktuelle Defaults; alte und unbekannte Felder werden ignoriert");
        check(new SettingsStore().Folder == Path.GetFullPath(SettingsStore.DefaultFolder), "Standard-Speicherort bleibt das Anwendungsverzeichnis");
        File.WriteAllText(Path.Combine(folder, "appsettings.json"), """{"SchemaVersion":999}""");
        bool rejected = false;
        try { store.Load(); } catch (InvalidDataException) { rejected = true; }
        check(rejected && File.ReadAllText(Path.Combine(folder, "appsettings.json")).Contains("999"), "Neuere unbekannte Einstellungsformate bleiben geschützt");
    }
}
