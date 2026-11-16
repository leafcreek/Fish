using Fish.Configuration;
using Fish.Core;

static class SensitivityTests
{
    public static void Run(Action<bool, string> check, string folder)
    {
        var defaults = new BotOptions();
        check(DetectionSensitivity.Bobber(defaults) == new BobberTuning(50, 50) &&
            DetectionSensitivity.Splash(defaults) == new SplashTuning(50, 10),
            "Neue Mittelstellung verwendet 50 Bewertung, 50 Farbtoleranz, 50 Rauschtoleranz und 10 % Mindeständerung");
        foreach (var reference in new[] { defaults })
        {
            var previousBobber = new BobberTuning(99, 0);
            var previousSplash = new SplashTuning(255, 80);
            bool monotonic = true;
            for (int level = 0; level <= 100; level++)
            {
                reference.BobberSensitivity = reference.SplashSensitivity = level;
                var bobber = DetectionSensitivity.Bobber(reference);
                var splash = DetectionSensitivity.Splash(reference);
                monotonic &= bobber.MinimumRating <= previousBobber.MinimumRating && bobber.ColorTolerance >= previousBobber.ColorTolerance &&
                    splash.PixelTolerance <= previousSplash.PixelTolerance && splash.MinimumChangePercent <= previousSplash.MinimumChangePercent;
                var runtime = FishingOptions.From(reference);
                monotonic &= runtime.BobberMatchMinimum == bobber.MinimumRating && runtime.BobberColorTolerance == bobber.ColorTolerance &&
                    runtime.Motion.PixelTolerance == splash.PixelTolerance && runtime.Motion.MinimumPercent == splash.MinimumChangePercent &&
                    runtime.Motion is { Adaptive: true, CalibrationMilliseconds: 1000 };
                previousBobber = bobber; previousSplash = splash;
            }
            check(monotonic, "Alle 101 Reglerstufen wirken in dieselbe Richtung und behalten Kalibrierung/Wasseranpassung bei");
            reference.BobberSensitivity = reference.SplashSensitivity = 50;
            check(DetectionSensitivity.Bobber(reference) == new BobberTuning(50, 50) &&
                DetectionSensitivity.Splash(reference) == new SplashTuning(50, 10),
                "Zurück auf 50 stellt die festen Basiswerte ohne Drift wieder her");
        }
        var savedFolder = Path.Combine(folder, "sensitivity");
        var store = new SettingsStore(savedFolder);
        Directory.CreateDirectory(savedFolder);
        File.WriteAllText(Path.Combine(savedFolder, "settings.json"), """
            {"SchemaVersion":5,"BobberMatchMinimum":37,"BobberColorTolerance":60,
             "SplashPixelTolerance":24,"SplashChangePercent":4,"WindowWidth":700,"WindowHeight":600}
            """);
        var migrated = store.Load();
        var migratedBobber = DetectionSensitivity.Bobber(migrated);
        var migratedSplash = DetectionSensitivity.Splash(migrated);
        check(migrated is { SchemaVersion: 7 } && Math.Abs(migratedBobber.MinimumRating - 37) <= 2 &&
            Math.Abs(migratedBobber.ColorTolerance - 60) <= 2 && migratedSplash == new SplashTuning(24, 4),
            "Alte Einzelwerte werden in die nächstpassenden Reglerstellungen übertragen");
        migrated.BobberSensitivity = 72; migrated.SplashSensitivity = 81;
        var snapshot = FishingOptions.From(migrated);
        for (int i = 0; i < 3; i++) { store.Save(migrated); migrated = store.Load(); }
        check(migrated is { BobberSensitivity: 72, SplashSensitivity: 81 } && FishingOptions.From(migrated) == snapshot,
            "Reglerpositionen überstehen mehrere Neustarts ohne erneute Verstärkung");
        migrated.BobberSensitivity = 0; migrated.SplashSensitivity = 100;
        check(FishingOptions.From(migrated) != snapshot, "Laufzeitschnappschuss bleibt von späteren Slider-Änderungen unabhängig");

        defaults.BobberSensitivity = 100;
        var maximum = DetectionSensitivity.Bobber(defaults);
        check(maximum.ColorTolerance == 90 && maximum.MinimumRating == 1,
            "Neue Bobber-Kurve endet bei Farbtoleranz 90 statt alle RGB-Farben freizugeben");
        using var stream = typeof(SensitivityTests).Assembly.GetManifestResourceStream("Fish.Tests.forever-forest.png")!;
        var source = TestBitmapDecoder.Read(stream);
        const int x = 176, y = 232, width = 784, height = 241;
        var pixels = new int[width * height];
        for (int row = 0; row < height; row++) Array.Copy(source.Pixels, (y + row) * source.Width + x, pixels, row * width, width);
        var image = new PixelFrame(width, height, pixels);
        defaults.BobberSensitivity = 70;
        var tuned = DetectionSensitivity.Bobber(defaults);
        var found = new BobberDetector().Find(image, tuned.MinimumRating, colorTolerance: tuned.ColorTolerance);
        check(found != null && new PixelRect(717 - x, 345 - y, 47, 52).Contains(found.Point),
            "Bobber-Regler 70 erkennt den beleuchteten Wald-Bobber mit einem Klickpunkt im Körper");
        bool Detects(int sensitivity, int difference)
        {
            var options = new BotOptions { SplashSensitivity = sensitivity };
            var detector = FishingOptions.From(options).Motion.CreateDetector();
            var start = DateTimeOffset.UnixEpoch;
            var calm = new PixelFrame(50, 50, new int[2500]);
            for (int i = 0; i <= 10; i++) detector.Analyze(calm, start.AddMilliseconds(i * 100), createPreview: false);
            var changed = new int[2500];
            Array.Fill(changed, difference << 16 | difference << 8 | difference, 0, 500);
            return detector.Analyze(new(50, 50, changed), start.AddMilliseconds(1100), createPreview: false).Measurement.NewTrigger;
        }
        check(!Detects(50, 30) && Detects(80, 30),
            "Höhere Splash-Empfindlichkeit erkennt schwache zusammenhängende Bewegung nach vollständiger Kalibrierung");
        check(Detects(50, 70) && !Detects(20, 70),
            "Geringere Splash-Empfindlichkeit filtert Bewegung heraus, die bei Mittelstellung auslöst");
    }
}
