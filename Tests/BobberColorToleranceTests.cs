using Fish.Core;

static class BobberColorToleranceTests
{
    public static void Run(Action<bool, string> check)
    {
        using var stream = typeof(BobberColorToleranceTests).Assembly.GetManifestResourceStream("Fish.Tests.forever-forest.png")!;
        var source = TestBitmapDecoder.Read(stream);
        const int x = 176, y = 232, width = 784, height = 241;
        var pixels = new int[width * height];
        for (int row = 0; row < height; row++) Array.Copy(source.Pixels, (y + row) * source.Width + x, pixels, row * width, width);
        var frame = new PixelFrame(width, height, pixels);
        var detector = new BobberDetector();
        foreach (int invalid in new[] { -1, 256 })
        {
            bool rejected = false;
            try { detector.Find(frame, 50, colorTolerance: invalid); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "Ungültige Farbtoleranz wird vor dem Scan abgewiesen");
        }
        var automatic = detector.Find(frame, 50, colorTolerance: 50);
        check(automatic != null && new PixelRect(717 - x, 345 - y, 47, 52).Contains(automatic.Point),
            "Wald-Screenshot: automatische Hintergrund- und Lichtanpassung findet den Körper bei mittlerer Toleranz");
        var found = detector.Find(frame, 80, colorTolerance: 60);
        check(found is { FeatureCount: >= 2 } && new PixelRect(717 - x, 345 - y, 47, 52).Contains(found.Point),
            "Wald-Screenshot: Farbtoleranz 60 erkennt getrennte Merkmale und einen Klickpunkt im Körper bei 80 % Bewertung");
        check(detector.Find(frame, 99, colorTolerance: 60) == null,
            "Eine hohe Mindestbewertung bleibt trotz passender Farbtoleranz wirksam");
    }
}
