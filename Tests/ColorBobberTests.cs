using Fish.Core;
using System.Diagnostics;
using System.Text.Json;

static class ColorBobberTests
{
    public static void Run(Action<bool, string> check, string[] args)
    {
        var profile = BobberColorProfile.Default;
        var measured = ReferenceProfile.Measure(ReferenceBobber.Default);
        check(profile == measured, "Gespeichertes Farbprofil entspricht den aus der aktuellen PNG gemessenen Werten");
        check(JsonSerializer.Deserialize<BobberColorProfile>(JsonSerializer.Serialize(profile)) == profile,
            "Alle vier Merkmalsreferenzen und ihre räumliche Anordnung lassen sich verlustfrei speichern");
        foreach (var invalid in new[] { profile with { Anchor = new(-1, 0) },
            profile with { Metal = null! }, profile with { Metal = profile.Metal with { Metallic = false } },
            profile with { Blue = profile.Blue with { Hex = "#GGGGGG" } } })
        {
            using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(invalid)));
            bool rejected = false;
            try { BobberColorProfile.Read(source); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Ungültige eingefrorene Schwimmer-Referenzen werden vor der Erkennung abgewiesen");
        }
        check(profile.Blue.Hex == "#2F364B" && profile.Red.Hex == "#4D170B" && profile.Body.Hex == "#76563D",
            "Blau, Rot und Körperbraun werden aus der PNG statt aus festen Such-RGB-Werten gewonnen");
        var detector = new BobberDetector();
        var template = ReferenceBobber.Default;
        foreach (var pose in new[] { (1.0, 0.0, 1.0, 0.0), (.6, -15.0, 1.0, .09), (1.4, 25.0, .9, -.09) })
        {
            var scene = BobberTestImages.Water(320, 260);
            var target = new PixelPoint(180, 150);
            BobberTestImages.WarpOnto(scene, template, target, pose.Item1, pose.Item2, pose.Item3, pose.Item4);
            var match = detector.Find(scene, 80);
            check(match != null && Math.Abs(match.Point.X - target.X) <= 7 && Math.Abs(match.Point.Y - target.Y) <= 7,
                $"Farbgruppen finden den Körper bei Größe/Drehung/Perspektive {pose}: {match}");
        }
        foreach (double brightness in new[] { .45, .7, 1.3 })
        {
            var pixels = template.Image.Pixels.Select(p => (p & unchecked((int)0xff000000)) |
                Math.Clamp((int)Math.Round(((p >> 16) & 255) * brightness), 0, 255) << 16 |
                Math.Clamp((int)Math.Round(((p >> 8) & 255) * brightness), 0, 255) << 8 |
                Math.Clamp((int)Math.Round((p & 255) * brightness), 0, 255)).ToArray();
            var scene = BobberTestImages.Water(260, 200);
            BobberTestImages.Paste(scene, new(template.Image.Width, template.Image.Height, pixels), 81, 61);
            check(detector.Find(scene, 80) != null, $"Farbgruppen erlauben Helligkeit {brightness:P0} ohne Änderung der Referenzfarben");
        }
        var fogPixels = template.Image.Pixels.Select(p => (p & unchecked((int)0xff000000)) |
            (int)Math.Round(((p >> 16) & 255) * .65 + 128 * .35) << 16 |
            (int)Math.Round(((p >> 8) & 255) * .65 + 128 * .35) << 8 |
            (int)Math.Round((p & 255) * .65 + 128 * .35)).ToArray();
        var fogScene = BobberTestImages.Water(260, 200);
        BobberTestImages.Paste(fogScene, new(template.Image.Width, template.Image.Height, fogPixels), 81, 61);
        check(detector.Find(fogScene, 80) != null, "Entsättigung durch einen simulierten grauen Nebelschleier bleibt erkennbar");
        BobberFeatures[] singleFeatures = [BobberFeatures.Blue, BobberFeatures.Red, BobberFeatures.Body, BobberFeatures.Metal];
        for (int first = 0; first < singleFeatures.Length; first++)
        {
            var singleScene = BobberTestImages.Water(260, 200);
            BobberTestImages.Paste(singleScene, BobberTestImages.OnlyFeatures(singleFeatures[first]), 81, 61);
            check(detector.Find(singleScene, 10) == null, $"Einzelmerkmal {singleFeatures[first]} reicht auch bei 10 % nicht aus");
            for (int second = first + 1; second < singleFeatures.Length; second++)
            {
                var pair = singleFeatures[first] | singleFeatures[second];
                var scene = BobberTestImages.Water(260, 200);
                BobberTestImages.Paste(scene, BobberTestImages.OnlyFeatures(pair), 81, 61);
                var candidate = detector.Find(scene, 80);
                check(candidate is { NeedsConfirmation: true } && candidate.Features == pair &&
                    Math.Abs(candidate.Point.X - (81 + template.Anchor.X)) <= 7 && Math.Abs(candidate.Point.Y - (61 + template.Anchor.Y)) <= 7,
                    $"Zwei von vier reichen, mit Klickpunkt im Körper und zeitlicher Bestätigung: {pair} · {candidate}");
            }
        }
        var separated = BobberTestImages.Water(400, 260);
        foreach (var pose in new[] { (.6, -15.0, 1.0, .09), (1.4, 25.0, .9, -.09) })
        {
            var scene = BobberTestImages.Water(320, 260);
            var pairTemplate = new ReferenceBobber(BobberTestImages.OnlyFeatures(BobberFeatures.Red | BobberFeatures.Metal), template.Anchor);
            BobberTestImages.WarpOnto(scene, pairTemplate, new(180, 150), pose.Item1, pose.Item2, pose.Item3, pose.Item4);
            var candidate = detector.Find(scene, 80);
            check(candidate is { NeedsConfirmation: true, Features: BobberFeatures.Red | BobberFeatures.Metal } &&
                Math.Abs(candidate.Point.X - 180) <= 9 && Math.Abs(candidate.Point.Y - 150) <= 9,
                $"Rot + Metall bleiben bei Größe, Drehung und Perspektive {pose} mit sicherem Klickpunkt erkennbar · {candidate}");
        }
        void Patch(PixelFrame scene, int x, int y, int width, int height, string hex)
        {
            int color = Convert.ToInt32(hex[1..], 16);
            for (int row = y; row < y + height; row++)
            for (int col = x; col < x + width; col++) scene.Pixels[row * scene.Width + col] = color;
        }
        Patch(separated, 260, 170, 30, 40, profile.Body.Hex);
        Patch(separated, 15, 15, 35, 20, profile.Blue.Hex);
        Patch(separated, 80, 40, 40, 18, profile.Red.Hex);
        check(detector.Find(separated, 10) == null, "Alle drei Farben an weit getrennten Stellen reichen nicht für einen Bobber");
        var inverted = BobberTestImages.Water(260, 240);
        Patch(inverted, 130, 85, 30, 40, profile.Body.Hex);
        Patch(inverted, 100, 170, 35, 20, profile.Blue.Hex);
        Patch(inverted, 65, 170, 40, 18, profile.Red.Hex);
        check(detector.Find(inverted, 10) == null, "Drei nahe Farbgruppen mit Federn unterhalb des Körpers werden verworfen");
        var noise = BobberTestImages.Water(300, 200);
        for (int i = 0; i < 300; i++)
            noise.Pixels[(i * 197 + 31) % noise.Pixels.Length] = Convert.ToInt32(new[] { profile.Blue.Hex, profile.Red.Hex, profile.Body.Hex }[i % 3][1..], 16);
        check(detector.Find(noise, 10) == null, "Verstreute einzelne Farbpixel werden nicht als zusammengehörige Gruppen erkannt");
        using var water = typeof(ColorBobberTests).Assembly.GetManifestResourceStream("Fish.Tests.forever-water.png")!;
        check(detector.Find(TestBitmapDecoder.Read(water), 10) == null, "Bekannter echter Wasser-Fehlertreffer enthält keinen gültigen Dreifarben-Bobber");
        using var real = typeof(ColorBobberTests).Assembly.GetManifestResourceStream("Fish.Tests.forever-feathered-bobber.png")!;
        var realMatch = detector.Find(TestBitmapDecoder.Read(real), 80);
        check(realMatch != null && new PixelRect(58, 58, 30, 38).Contains(realMatch.Point), $"Echter Bobber wird mit Klickpunkt im Körper gefunden: {realMatch}");
        foreach (string fixture in new[] { "forever-water-cursor.png", "forever-shore.png" })
        {
            using var negative = typeof(ColorBobberTests).Assembly.GetManifestResourceStream($"Fish.Tests.{fixture}")!;
            var negativeMatch = detector.Find(TestBitmapDecoder.Read(negative), 10);
            check(negativeMatch == null,
                $"Echter großer Ausschnitt ohne Bobber bleibt auch bei 10 % ohne Treffer: {fixture} · {negativeMatch}");
        }
        using var realArea = typeof(ColorBobberTests).Assembly.GetManifestResourceStream("Fish.Tests.forever-bobber-area.png")!;
        var areaMatch = detector.Find(TestBitmapDecoder.Read(realArea), 80);
        check(areaMatch != null && new PixelRect(435, 196, 32, 40).Contains(areaMatch.Point),
            "Der echte Bobber wird auch im großen Suchbereich unter weiteren Wasserfarben korrekt lokalisiert");
        foreach (var fixture in new[] { ("forever-sunset.png", new PixelRect(467, 171, 31, 30)),
            ("forever-sunset-area.png", new PixelRect(302, 64, 31, 30)) })
        {
            using var sunset = typeof(ColorBobberTests).Assembly.GetManifestResourceStream($"Fish.Tests.{fixture.Item1}")!;
            var sunsetMatch = detector.Find(TestBitmapDecoder.Read(sunset), 80);
            check(sunsetMatch != null && sunsetMatch.Features.HasFlag(BobberFeatures.Blue | BobberFeatures.Body) && fixture.Item2.Contains(sunsetMatch.Point),
                $"Sonnenuntergang: Körper und Blau werden trotz mit Wasser verschmolzenem Rot gefunden: {fixture.Item1} · {sunsetMatch}");
        }
        using var night = typeof(ColorBobberTests).Assembly.GetManifestResourceStream("Fish.Tests.forever-night.png")!;
        var nightFrame = TestBitmapDecoder.Read(night);
        foreach (int nightMinimum in new[] { 10, 80 })
        {
            var nightMatch = detector.Find(nightFrame, nightMinimum);
            check(nightMatch is { Features: BobberFeatures.Red | BobberFeatures.Metal, NeedsConfirmation: true } &&
                new PixelRect(255, 150, 25, 31).Contains(nightMatch.Point),
                $"Nacht-Screenshot: Rot + Metall reichen trotz olivem Körper und blauem Wasser bei {nightMinimum} % · {nightMatch}");
        }
        using var oldBody = typeof(ColorBobberTests).Assembly.GetManifestResourceStream("Fish.Tests.forever-body.png")!;
        var bodyOnly = BobberTestImages.Water(240, 180);
        BobberTestImages.Paste(bodyOnly, TestBitmapDecoder.Read(oldBody), 91, 63);
        check(detector.Find(bodyOnly, 10) == null, "Ein reiner Körper ohne zweites Merkmal reicht nicht für einen Treffer");
        var edge = BobberTestImages.Water(220, 180);
        BobberTestImages.Paste(edge, template.Image, 0, 0);
        var edgeMatch = detector.Find(edge, 80)!;
        check(edgeMatch != null && edgeMatch.Bounds.X == 0 && edgeMatch.Bounds.Y == 0,
            "Alle drei Farbgruppen werden auch unmittelbar am Rand des Suchbereichs gefunden");
        var marked = BobberPreview.Mark(edge, edgeMatch!);
        check(!marked.Pixels.SequenceEqual(edge.Pixels) && marked.Pixels[edgeMatch!.Point.Y * edge.Width + edgeMatch.Point.X] == 0x00ff80,
            "Fundmarkierung zeigt den Körper-Klickpunkt und verändert das Rohbild nicht");
        using var stopped = new CancellationTokenSource(); stopped.Cancel();
        bool canceled = false;
        try { detector.Find(separated, 80, stopped.Token); } catch (OperationCanceledException) { canceled = true; }
        check(canceled, "Farbgruppen-Suche respektiert Stop vor dem Scan");
        using var duringScan = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
        canceled = false;
        try { detector.Find(new(2000, 1200, new int[2000 * 1200]), 80, duringScan.Token); }
        catch (OperationCanceledException) { canceled = true; }
        check(canceled, "Stop unterbricht auch eine laufende große Farbgruppen-Suche");
        var standard = BobberTestImages.Water(800, 400);
        BobberTestImages.Paste(standard, template.Image, 213, 131);
        var timer = Stopwatch.StartNew();
        var found = detector.Find(standard, 80);
        Console.WriteLine($"COLOR 800 × 400: {timer.Elapsed.TotalMilliseconds:F1} ms");
        check(found != null, "Farbgruppen finden den Bobber im vollständigen Standardsuchbereich");
        int minimumIndex = Array.IndexOf(args, "--bobber-minimum");
        int minimum = minimumIndex >= 0 ? int.Parse(args[minimumIndex + 1]) : 80;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is not ("--bobber-positive" or "--bobber-negative")) continue;
            bool positive = args[i] == "--bobber-positive";
            using var stream = File.OpenRead(args[++i]);
            var result = detector.Find(TestBitmapDecoder.Read(stream), minimum);
            check((result != null) == positive, $"Echter Screenshot {(positive ? "mit" : "ohne")} Bobber bei {minimum} %: {args[i]} · {result}");
        }
    }
}
