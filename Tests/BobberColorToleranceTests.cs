using Fish.Core;

static class BobberColorToleranceTests
{
    public static void Run(Action<bool, string> check)
    {
        var profile = BobberColorProfile.Default;
        foreach (var (signature, pixel, distance) in new[] {
            (profile.Blue, 0x4D364B, 30), (profile.Red, 0x4D350B, 30),
            (profile.Body, 0x76743D, 30), (profile.Metal, 0xB479B5, 60) })
        {
            var hsv = HsvColor.FromRgb(pixel);
            double admitted = new BobberColorMatcher(signature, distance).Similarity(pixel, hsv);
            check(signature.Similarity(hsv) == 0 &&
                new BobberColorMatcher(signature, distance - 1).Similarity(pixel, hsv) == 0 && admitted > 0,
                $"RGB-Toleranz lässt {signature.Name} genau ab der nötigen Kanalabweichung {distance} zu");
            check(new BobberColorMatcher(signature, 255).Similarity(pixel, hsv) == admitted,
                $"Höhere Toleranz erhöht nicht die Qualität desselben Pixels: {signature.Name}");
            foreach (int extreme in new[] { 0, 0xffffff, 0xff0000, 0x00ff00, 0x0000ff })
                check(new BobberColorMatcher(signature, 255).Similarity(extreme, HsvColor.FromRgb(extreme)) > 0,
                    $"Toleranz 255 erlaubt jeden RGB-Kanalwert für {signature.Name}: {extreme:X6}");
        }
        using var stream = typeof(BobberColorToleranceTests).Assembly.GetManifestResourceStream("Fish.Tests.forever-forest.png")!;
        var source = TestBitmapDecoder.Read(stream);
        const int x = 176, y = 232, width = 784, height = 241;
        var pixels = new int[width * height];
        for (int row = 0; row < height; row++) Array.Copy(source.Pixels, (y + row) * source.Width + x, pixels, row * width, width);
        var frame = new PixelFrame(width, height, pixels);
        var detector = new BobberDetector();
        check(detector.Find(frame, 1) == null, "Wald-Screenshot: alte Farbtoleranz scheitert unabhängig von der Mindestbewertung");
        var found = detector.Find(frame, 80, colorTolerance: 60);
        check(found is { NeedsConfirmation: true } && new PixelRect(717 - x, 345 - y, 47, 52).Contains(found.Point),
            "Wald-Screenshot: RGB-Toleranz 60 erkennt zwei Merkmale und einen Klickpunkt im Körper bei 80 % Bewertung");
        check(detector.Find(frame, 99, colorTolerance: 60) == null,
            "Eine hohe Mindestbewertung bleibt trotz passender Farbtoleranz wirksam");
    }
}
