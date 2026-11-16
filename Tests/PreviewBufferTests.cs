using Fish.Core;

static class PreviewBufferTests
{
    public static void Run(Action<bool, string> check)
    {
        var previewBuffer = new PreviewBuffer();
        var previewPixels = new int[800 * 400];
        previewPixels[0] = 0xff0000;
        previewBuffer.Add(new(800, 400, previewPixels), PreviewKind.Bobber, true);
        previewPixels[0] = 0;
        var preview = previewBuffer.Take();
        check(preview.Bobber is { Width: 480, Height: 240 } && preview.Bobber.Pixels[0] == unchecked((int)0xffff0000), "Vorschau bewahrt Seitenverhältnis, kopiert Pixel und setzt Alpha");
        previewBuffer.Add(new(2, 1, [0xffffff, 0]), PreviewKind.Splash, true);
        check(previewBuffer.Take().Splash?.Pixels[0] == unchecked((int)0xffffffff), "Splash-Fund bleibt unabhängig vom Live-Limit erhalten");
        check(previewBuffer.Take() == (null, null, null), "Vorschaupuffer gibt konsumierte Bilder frei");
        previewBuffer.Add(new(2, 1, [0xffffff, 0]), PreviewKind.Bobber, false, includeLive: false);
        check(previewBuffer.Take() == (null, null, null), "Verborgene Live-Vorschau erzeugt keine gepufferte Bildkopie");
        previewBuffer.Add(new(2, 1, [0xffffff, 0]), PreviewKind.Bobber, true, includeLive: false);
        previewBuffer.Add(new(2, 1, [0, 0xffffff]), PreviewKind.Splash, true, includeLive: false);
        var hidden = previewBuffer.Take();
        check(hidden.Live == null && hidden.Bobber != null && hidden.Splash != null,
            "Bestätigte Bobber- und Splash-Fundbilder bleiben bei ausgeblendeter Live-Vorschau erhalten");
        previewBuffer.Add(new(2, 1, [0xffffff, 0]), PreviewKind.Splash, true);
        previewBuffer.ClearLive();
        var paused = previewBuffer.Take();
        check(paused.Live == null && paused.Splash != null, "Verbergen der Liveansicht behält das Spritzer-Fundbild ohne Messwerttransport");
    }
}
