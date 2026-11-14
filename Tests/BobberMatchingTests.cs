using Fish.Core;
using System.Diagnostics;

static class BobberMatchingTests
{
    public static void Run(Action<bool, string> check, string[] args)
    {
        var template = BobberTemplate.Default;
        var matcher = new BobberTemplateMatcher(template);
        check(template.Image is { Width: 41, Height: 69 } && template.UseAlpha,
            "Transparente Forever-PNG wird mit Alphamaske eingebettet geladen");
        check(template.Image.Pixels.Count(p => (uint)p >> 24 == 0) == 1147 &&
            template.Image.Pixels.Count(p => (uint)p >> 24 is > 0 and < 255) == 605,
            "PNG-Decoder erhält vollständig und teilweise transparente Pixel");

        var scene = Water(320, 220);
        Paste(scene, template.Image, 53, 47);
        var match = matcher.Find(scene, 80);
        check(match is { Confidence: > .9, Scale: 1 } && match.Bounds == new PixelRect(53, 47, 41, 69) &&
            match.Point == new PixelPoint(53 + template.Anchor.X, 47 + template.Anchor.Y), $"Template findet Körpermitte außerhalb des Suchrasters: {match}");
        var black = new PixelFrame(800, 400, new int[800 * 400]);
        Paste(black, template.Image, 23, 31);
        var scanTimer = Stopwatch.StartNew();
        var blackMatch = matcher.Find(black, 80);
        Console.WriteLine($"SCAN 800 × 400: {scanTimer.Elapsed.TotalMilliseconds:F1} ms");
        check(blackMatch?.Point == new PixelPoint(23 + template.Anchor.X, 31 + template.Anchor.Y),
            $"Schwimmerkörper bleibt auf schwarzem Hintergrund korrekt lokalisiert: {blackMatch}");
        var hiddenRgb = new PixelFrame(41, 69, template.Image.Pixels.Select(p => (uint)p >> 24 == 0 ? 0x00ff00ff : p).ToArray());
        var hiddenRgbMatch = new BobberTemplateMatcher(template with { Image = hiddenRgb }).Find(scene, 80);
        check(hiddenRgbMatch == match, "Unsichtbare RGB-Werte beeinflussen weder Skalierung noch Trefferwert");
        bool emptyMaskRejected = false;
        try { _ = new BobberTemplateMatcher(new(new(16, 16, new int[256]), new(8, 8), UseAlpha: true)); }
        catch (ArgumentException) { emptyMaskRejected = true; }
        check(emptyMaskRejected, "Vollständig transparente Bildvorlage wird abgewiesen");
        var replacementPixels = new int[20 * 30];
        for (int y = 15; y < 25; y++)
        for (int x = 4; x < 14; x++) replacementPixels[y * 20 + x] = unchecked((int)0xff887755);
        check(BobberTemplate.FromImage(new(20, 30, replacementPixels)).Anchor == new PixelPoint(8, 20),
            "Andere PNG-Größe erhält automatisch einen Anker im sichtbaren Schwimmerkörper");
        var marked = BobberTemplateMatcher.Mark(scene, match!);
        check(marked.Pixels[47 * scene.Width + 53] == 0x00ff80 && scene.Pixels[47 * scene.Width + 53] != 0x00ff80,
            "Fundmarkierung verändert den Aufnahme- und Vergleichspuffer nicht");

        var brighter = new PixelFrame(41, 69, template.Image.Pixels.Select(p => (p & unchecked((int)0xff000000)) |
            Math.Min(255, ((p >> 16) & 255) + 25) << 16 | Math.Min(255, ((p >> 8) & 255) + 25) << 8 | Math.Min(255, (p & 255) + 25)).ToArray());
        scene = Water(260, 180);
        Paste(scene, brighter, 121, 71);
        match = matcher.Find(scene, 80);
        check(match is { Confidence: > .9 } && Math.Abs(match.Point.X - 121 - template.Anchor.X) <= 2 &&
            Math.Abs(match.Point.Y - 71 - template.Anchor.Y) <= 2,
            $"Normalisierter Bildvergleich verträgt geänderte Helligkeit: {match}");

        foreach (double scale in new[] { .6, .8, 1.2, 1.4 })
        {
            var resized = Resize(template.Image, scale);
            scene = Water(260, 190);
            Paste(scene, resized, scene.Width - resized.Width, scene.Height - resized.Height);
            match = matcher.Find(scene, 80);
            int expectedX = scene.Width - resized.Width + (int)Math.Round(template.Anchor.X * scale);
            int expectedY = scene.Height - resized.Height + (int)Math.Round(template.Anchor.Y * scale);
            check(match != null && Math.Abs(match.Point.X - expectedX) <= 3 && Math.Abs(match.Point.Y - expectedY) <= 3,
                $"Template erkennt Größe {scale:P0} am rechten und unteren Bildrand: {match}");
        }
        foreach (var pose in new[] { (1.0, 30.0, 1.0, 0.0), (.8, -30.0, 1.0, 0.0),
            (1.0, 15.0, .8, .18), (1.2, -15.0, 1.2, -.18), (.95, 22.0, .9, .12) })
        {
            scene = Water(280, 220);
            var target = new PixelPoint(153, 127);
            WarpOnto(scene, template, target, pose.Item1, pose.Item2, pose.Item3, pose.Item4);
            match = matcher.Find(scene, 80);
            check(match != null && Math.Abs(match.Point.X - target.X) <= 3 && Math.Abs(match.Point.Y - target.Y) <= 3 &&
                Math.Abs(match.RotationDegrees - pose.Item2) <= 15,
                $"Unabhängig gerendertes Ziel: Größe {pose.Item1:P0}, Drehung {pose.Item2}°, Breite {pose.Item3:P0}, Perspektive {pose.Item4:P0}: {match}");
        }
        scene = Water(280, 220);
        WarpOnto(scene, template, new(25, 127), 1, 30, 1, 0);
        var edgeMatch = matcher.Find(scene, 80);
        check(edgeMatch != null && Math.Abs(edgeMatch.Point.X - 25) <= 3 && Math.Abs(edgeMatch.Point.Y - 127) <= 3 &&
            edgeMatch.Bounds.X == 0 && BobberTemplateMatcher.Mark(scene, edgeMatch).Pixels.Length == scene.Pixels.Length,
            $"Transparente gedrehte Ecke darf außerhalb des Suchbereichs liegen: {edgeMatch}");
        scene = Water(320, 220);
        // A damaged reference precedes the actual bobber in scan order.
        var damaged = new PixelFrame(41, 69, (int[])template.Image.Pixels.Clone());
        for (int y = 0; y < damaged.Height; y++)
        for (int x = 0; x < 14; x++) damaged.Pixels[y * damaged.Width + x] = unchecked((int)0xff474141);
        Paste(scene, damaged, 0, 0);
        Paste(scene, template.Image, 213, 131);
        match = matcher.Find(scene, 80);
        check(match?.Bounds == new PixelRect(213, 131, 41, 69), "Bester Bildtreffer gewinnt vor früherem schwächeren Treffer");

        scene = Water(320, 220);
        for (int i = 0; i < 25; i++) scene.Pixels[(i * 2239 + 17) % scene.Pixels.Length] = 0xffffff;
        for (int y = 40; y < 60; y++)
        for (int x = 80; x < 120; x++) scene.Pixels[y * scene.Width + x] = 0xff0000;
        check(matcher.Find(scene, 80) == null, "Wasser, Schneepixel und rote Fläche erzeugen keinen Bobber-Treffer");
        using (var waterStream = typeof(BobberMatchingTests).Assembly.GetManifestResourceStream("Fish.Tests.forever-water.png")!)
        {
            var water = Fish.Native.BitmapDecoder.Read(waterStream);
            check(matcher.Find(water, 70) == null, "Echter Wasser-Fehlertreffer wird durch die Körperfarbprüfung verworfen");
        }
        check(matcher.Find(new(160, 120, Enumerable.Repeat(0x555555, 160 * 120).ToArray()), 10) == null,
            "Kontrastlose Fläche liefert auch bei niedriger Schwelle keinen Treffer");
        check(matcher.Find(new(10, 10, new int[100]), 80) == null, "Zu kleiner Suchbereich liefert keinen Treffer");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        bool stopped = false;
        try { matcher.Find(scene, 80, canceled.Token); } catch (OperationCanceledException) { stopped = true; }
        check(stopped, "Template Matching respektiert Stop vor dem Scan");
        using var scanCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        stopped = false;
        try { matcher.Find(black, 80, scanCancellation.Token); } catch (OperationCanceledException) { stopped = true; }
        check(stopped, "Stop bricht auch die laufende Suche nach gedrehten Vorlagen ab");

        // Optional real screenshot crops prepared outside the test binary. No game input is sent.
        int minimumIndex = Array.IndexOf(args, "--bobber-minimum");
        int minimum = minimumIndex >= 0 ? int.Parse(args[minimumIndex + 1]) : 80;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is not ("--bobber-positive" or "--bobber-negative")) continue;
            bool positive = args[i] == "--bobber-positive";
            using var stream = File.OpenRead(args[++i]);
            var frame = Fish.Native.BitmapDecoder.Read(stream);
            var timer = Stopwatch.StartNew();
            match = matcher.Find(frame, minimum);
            timer.Stop();
            Console.WriteLine($"MATCH {timer.Elapsed.TotalMilliseconds:F1} ms · {match}");
            check((match != null) == positive, $"Screenshot {(positive ? "mit" : "ohne")} Bobber bei {minimum} %: {args[i]}");
        }
    }

    public static PixelFrame Water(int width, int height)
    {
        var pixels = new int[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int wave = (int)(6 * Math.Sin(x * .07 + y * .4) + 3 * Math.Sin(y * .1));
            pixels[y * width + x] = (64 + wave) << 16 | (60 + wave) << 8 | (61 + wave);
        }
        return new(width, height, pixels);
    }

    public static void Paste(PixelFrame destination, PixelFrame source, int x, int y)
    {
        for (int row = 0; row < source.Height; row++)
        for (int column = 0; column < source.Width; column++)
        {
            int pixel = source.Pixels[row * source.Width + column];
            int alpha = (int)((uint)pixel >> 24);
            int index = (row + y) * destination.Width + x + column;
            int background = destination.Pixels[index], blended = 0;
            for (int shift = 0; shift <= 16; shift += 8)
                blended |= ((((pixel >> shift) & 255) * alpha + ((background >> shift) & 255) * (255 - alpha) + 127) / 255) << shift;
            destination.Pixels[index] = blended;
        }
    }

    // Independent forward renderer: supersample source pixels and deposit their projected areas in the scene.
    // Production matching instead samples the template through the inverse projective map.
    private static void WarpOnto(PixelFrame scene, BobberTemplate template, PixelPoint target,
        double scale, double angle, double aspect, double perspective)
    {
        double cx = (template.Image.Width - 1) / 2.0, cy = (template.Image.Height - 1) / 2.0;
        double radians = angle * Math.PI / 180;
        (double X, double Y) Project(double x, double y)
        {
            double depth = 1 + perspective * (y - cy) / cy;
            double u = (x - cx) * scale * aspect / depth, v = (y - cy) * scale / depth;
            return (u * Math.Cos(radians) - v * Math.Sin(radians), u * Math.Sin(radians) + v * Math.Cos(radians));
        }
        var anchor = Project(template.Anchor.X, template.Anchor.Y);
        var coverage = new double[scene.Pixels.Length];
        var colors = new double[scene.Pixels.Length * 3];
        for (int y = 0; y < template.Image.Height; y++)
        for (int x = 0; x < template.Image.Width; x++)
        {
            int pixel = template.Image.Pixels[y * template.Image.Width + x];
            double alpha = ((uint)pixel >> 24) / 255.0;
            if (alpha == 0) continue;
            for (int sy = 0; sy < 4; sy++)
            for (int sx = 0; sx < 4; sx++)
            {
                double sourceX = x + (sx + .5) / 4 - .5, sourceY = y + (sy + .5) / 4 - .5;
                var point = Project(sourceX, sourceY);
                int dx = (int)Math.Round(target.X + point.X - anchor.X), dy = (int)Math.Round(target.Y + point.Y - anchor.Y);
                if (dx < 0 || dy < 0 || dx >= scene.Width || dy >= scene.Height) continue;
                double depth = 1 + perspective * (sourceY - cy) / cy;
                double weight = alpha * scale * scale * aspect / (16 * depth * depth * depth);
                int index = dy * scene.Width + dx;
                coverage[index] += weight;
                for (int channel = 0; channel < 3; channel++) colors[index * 3 + channel] += weight * ((pixel >> (8 * channel)) & 255);
            }
        }
        for (int i = 0; i < scene.Pixels.Length; i++)
        {
            if (coverage[i] == 0) continue;
            double alpha = Math.Min(1, coverage[i]);
            int blended = 0;
            for (int channel = 0; channel < 3; channel++) blended |= (int)Math.Round(
                colors[i * 3 + channel] / coverage[i] * alpha + ((scene.Pixels[i] >> (8 * channel)) & 255) * (1 - alpha)) << (8 * channel);
            scene.Pixels[i] = blended;
        }
    }

    private static PixelFrame Resize(PixelFrame frame, double scale)
    {
        // Preserve the requested geometric scale between pixel centres; endpoint stretching changes small images' scale.
        int width = (int)Math.Ceiling((frame.Width - 1) * scale) + 1, height = (int)Math.Ceiling((frame.Height - 1) * scale) + 1;
        var pixels = new int[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            pixels[y * width + x] = frame.Pixels[Math.Min(frame.Height - 1, (int)Math.Round(y / scale)) * frame.Width +
                Math.Min(frame.Width - 1, (int)Math.Round(x / scale))];
        return new(width, height, pixels);
    }
}
