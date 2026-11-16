using Fish.Core;
using System.Diagnostics;

static class SplashMotionTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Run(Action<bool, string> check)
    {
        var water = Frame(100, 100, 0x405060);
        var detector = new SplashMotionDetector(20, 5, false, 500);
        var first = detector.Analyze(water, Start);
        check(!first.Measurement.Ready && !first.Measurement.NewTrigger && first.Measurement.ChangedPercent == 0,
            "Erstes Splash-Bild ist nur Referenz");
        detector.Analyze(water, Start.AddMilliseconds(150));
        detector.Analyze(water, Start.AddMilliseconds(300));
        check(!detector.Analyze(water, Start.AddMilliseconds(450)).Measurement.Ready,
            "Keine Bisserkennung vor Ablauf der Kalibrierung");
        check(detector.Analyze(water, Start.AddMilliseconds(600)).Measurement.Ready,
            "Stabile Referenz wird nach Kalibrierzeit und mindestens drei Vergleichen bereit");
        var bite = Block(water, 20, 20, 30, 20, 0x586878); // Different by 24; same general water colour.
        var result = detector.Analyze(bite, Start.AddMilliseconds(750));
        check(result.Measurement.NewTrigger && result.Measurement.ChangedPercent == 6 && result.Measurement.LargestComponentPercent == 6,
            "Farbähnlicher Splash: 600 von 10000 Pixeln ergeben genau 6 %");
        check(result.Preview.Pixels[2020] != bite.Pixels[2020] && bite.Pixels[2020] == 0x586878,
            "Änderungsmarkierung verändert das Rohbild nicht");
        result = detector.Analyze(bite, Start.AddMilliseconds(900));
        check(result.Measurement.ChangedPercent == 0 && !result.Measurement.NewTrigger,
            "Nächster Vergleich verwendet das vorherige Rohbild ohne rote Markierung");
        check(!detector.Analyze(water, Start.AddMilliseconds(1050)).Measurement.NewTrigger &&
            !detector.Analyze(bite, Start.AddMilliseconds(1200)).Measurement.NewTrigger,
            "Verschwinden und erneutes Auftauchen des Splashs erzeugen keinen zweiten Treffer");

        detector.Reset(); Warm(detector, water);
        check(detector.Analyze(bite, Start.AddMilliseconds(750)).Measurement.NewTrigger,
            "Neuer Wurf setzt die Auslösesperre zurück");

        detector = new(20, 1, false, 500); Warm(detector, water);
        var noise = Block(water, 20, 20, 10, 10, 0x536373); // Difference 19, below noise tolerance.
        check(detector.Analyze(noise, Start.AddMilliseconds(750)).Measurement.ChangedPercent == 0,
            "Kleine RGB-Änderungen unter der Rauschtoleranz zählen nicht");
        var boundary = Block(water, 20, 20, 10, 10, 0x546474);
        check(detector.Analyze(boundary, Start.AddMilliseconds(900)).Measurement.ChangedPercent == 0,
            "Verglichen wird mit dem unmittelbar vorherigen Bild, nicht mit dem Wurfanfang");
        detector.Reset(); Warm(detector, water);
        check(detector.Analyze(boundary, Start.AddMilliseconds(750)).Measurement.NewTrigger,
            "Rauschtoleranz und Prozentgrenze sind inklusive");

        detector = new(20, 5, false, 500); Warm(detector, water);
        var snow = Frame(100, 100, 0x405060);
        for (int y = 0; y < 100; y += 3)
        for (int x = 0; x < 100; x += 3) snow.Pixels[y * 100 + x] = 0xffffff;
        result = detector.Analyze(snow, Start.AddMilliseconds(750));
        check(result.Measurement.ChangedPercent > 10 && result.Measurement.LargestComponentPercent == .01 && !result.Measurement.NewTrigger,
            "Viele isolierte Schneepixel lösen trotz ausreichendem Gesamtanteil nicht aus");

        detector = new(20, 5, true, 500);
        var waves = Block(water, 0, 0, 20, 20, 0x8090a0);
        detector.Analyze(water, Start);
        for (int i = 1; i <= 4; i++) detector.Analyze(i % 2 == 0 ? water : waves, Start.AddMilliseconds(i * 150));
        result = detector.Analyze(waves, Start.AddMilliseconds(750));
        check(!result.Measurement.NewTrigger && result.Measurement.BaselinePercent == 4 && result.Measurement.ThresholdPercent == 12,
            "Adaptive Grenze berücksichtigt normale Wasserbewegung");
        var surge = Block(water, 0, 0, 60, 50, 0x8090a0);
        result = detector.Analyze(surge, Start.AddMilliseconds(900));
        check(result.Measurement.NewTrigger && result.Measurement.ChangedPercent == 26,
            "Plötzlicher Bewegungsanstieg über der Wasser-Grundbewegung löst aus");

        detector = new(20, 5, true, 500); Warm(detector, water);
        result = detector.Analyze(Frame(100, 100, 0xc0d0e0), Start.AddMilliseconds(750));
        check(result.Measurement.SceneChanged && !result.Measurement.Ready && !result.Measurement.NewTrigger,
            "Großflächiger Bildwechsel verwirft die Referenz ohne Loot auszulösen");
        check(!detector.Analyze(Frame(100, 100, 0xc0d0e0), Start.AddMilliseconds(900)).Measurement.Ready,
            "Nach Bildwechsel erneut kalibrieren");
        detector.Reset(); Warm(detector, water);
        detector.Analyze(bite, Start.AddMilliseconds(750));
        detector.Analyze(Frame(100, 100, 0xc0d0e0), Start.AddMilliseconds(900));
        check(detector.Analyze(water, Start.AddMilliseconds(1050)).Measurement.Triggered,
            "Ein Bildwechsel hebt eine bereits gesetzte Auslösesperre nicht auf");
        var resized = detector.Analyze(Frame(50, 200, 0x405060), Start.AddMilliseconds(1200));
        check(!resized.Measurement.Ready && resized.Measurement.ChangedPercent == 0,
            "Gleiche Pixelanzahl bei anderer Bildgeometrie benötigt eine neue Referenz");

        detector.Reset();
        var mutable = Frame(100, 100, 0x405060);
        detector.Analyze(mutable, Start);
        Array.Fill(mutable.Pixels, 0xffffff);
        check(detector.Analyze(water, Start.AddMilliseconds(150)).Measurement.ChangedPercent == 0,
            "Referenz bleibt nach einer Änderung des vom Aufrufer übergebenen Bildpuffers erhalten");

        detector = new(20, 5, true, 500);
        var small = Frame(120, 100, 0x405060);
        Warm(detector, small);
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) detector.Analyze(small, Start.AddSeconds(i + 1));
        Console.WriteLine($"MOTION 120 × 100: {timer.Elapsed.TotalMilliseconds / 100:F3} ms/Scan");
        var noPreview = new SplashMotionDetector(20, 5, false, 500);
        Warm(noPreview, small);
        var changed = Block(small, 10, 10, 30, 30, 0xffffff);
        var measurementOnly = noPreview.Analyze(changed, Start.AddSeconds(5), createPreview: false);
        check(measurementOnly.Measurement.NewTrigger && ReferenceEquals(measurementOnly.Preview, changed),
            "Ohne Vorschau bleibt die Bisserkennung aktiv und erzeugt keine markierte Bildkopie");
        var foundOnly = new SplashMotionDetector(20, 5, false, 500);
        Warm(foundOnly, small);
        var hiddenOrdinary = foundOnly.Analyze(small, Start.AddSeconds(4), createPreview: false, previewOnTrigger: true);
        var hiddenBite = foundOnly.Analyze(changed, Start.AddSeconds(5), createPreview: false, previewOnTrigger: true);
        check(ReferenceEquals(hiddenOrdinary.Preview, small) && hiddenBite.Measurement.NewTrigger &&
            !ReferenceEquals(hiddenBite.Preview, changed) && hiddenBite.Preview.Pixels[10 * changed.Width + 10] != changed.Pixels[10 * changed.Width + 10],
            "Ohne Live-Vorschau wird nur beim Biss eine rot markierte Fundbildkopie erzeugt");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        bool stopped = false;
        try { noPreview.Analyze(changed, Start.AddSeconds(6), cancelled.Token); }
        catch (OperationCanceledException) { stopped = true; }
        check(stopped, "Splash-Vergleich respektiert Stop vor jeder Auswertung");
        RunLiveTuning(check);
    }

    private static void RunLiveTuning(Action<bool, string> check)
    {
        var water = Frame(100, 100, 0x405060);
        var waves = Block(water, 0, 0, 20, 20, 0x586878); // 4% change below the original noise cutoff.
        var detector = new SplashMotionDetector(50, 5, true, 500);
        detector.Analyze(water, Start);
        for (int i = 1; i <= 4; i++) detector.Analyze(i % 2 == 0 ? water : waves, Start.AddMilliseconds(i * 150));
        detector.UpdateThresholds(20, 1);
        var result = detector.Analyze(waves, Start.AddMilliseconds(750)).Measurement;
        check(result.Ready && !result.NewTrigger && result.BaselinePercent == 4 && result.ThresholdPercent == 12,
            "Neue Rauschtoleranz bewertet gelernte Wasserbewegung sofort neu, ohne Kalibrierung oder Fehlbiss");
        detector.UpdateThresholds(80, 1);
        result = detector.Analyze(water, Start.AddMilliseconds(900)).Measurement;
        check(result.Ready && result.BaselinePercent == 0 && !result.NewTrigger,
            "Strengere Rauschtoleranz passt die Wasserbasis an und erhält die fertige Kalibrierung");

        var bite = Block(water, 0, 0, 50, 50, 0xffffff);
        check(detector.Analyze(bite, Start.AddMilliseconds(1050)).Measurement.NewTrigger,
            "Biss mit neuen Werten wird ohne Wartezeit erkannt");
        detector.UpdateThresholds(20, 1);
        check(!detector.Analyze(water, Start.AddMilliseconds(1200)).Measurement.NewTrigger,
            "Verstellen hebt eine bereits ausgelöste Bisssperre nicht auf");

        detector = new(50, 5, true, 500);
        detector.Analyze(water, Start); detector.Analyze(water, Start.AddMilliseconds(150));
        detector.UpdateThresholds(20, 1);
        detector.Analyze(water, Start.AddMilliseconds(300));
        check(detector.Analyze(water, Start.AddMilliseconds(600)).Measurement.Ready,
            "Verstellen während der Kalibrierung erhält die bereits vergangene Zeit");
    }

    private static void Warm(SplashMotionDetector detector, PixelFrame frame)
    {
        detector.Analyze(frame, Start);
        for (int i = 1; i <= 4; i++) detector.Analyze(frame, Start.AddMilliseconds(i * 150));
    }

    private static PixelFrame Frame(int width, int height, int pixel)
        => new(width, height, Enumerable.Repeat(pixel, width * height).ToArray());

    private static PixelFrame Block(PixelFrame frame, int x, int y, int width, int height, int pixel)
    {
        var copy = new PixelFrame(frame.Width, frame.Height, (int[])frame.Pixels.Clone());
        for (int row = y; row < y + height; row++)
        for (int column = x; column < x + width; column++) copy.Pixels[row * copy.Width + column] = pixel;
        return copy;
    }
}
