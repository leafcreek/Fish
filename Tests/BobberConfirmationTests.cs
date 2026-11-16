using Fish.Core;

static class BobberConfirmationTests
{
    public static void Run(Action<bool, string> check)
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var gap = TimeSpan.FromMilliseconds(750);
        var confirmation = new BobberConfirmation();
        var blue = new BobberMatch(new(20, 20, 80, 80), new(75, 75), .92, 1, Features: (BobberFeatures.Blue | BobberFeatures.Body));
        BobberMatch? Observe(BobberMatch? match, int ms) => confirmation.Observe(match, start.AddMilliseconds(ms), gap);
        check(Observe(blue, 0) == null && Observe(blue with { Point = new(77, 76) }, 150) == null &&
            Observe(blue with { Point = new(76, 78) }, 300) != null,
            "Zwei Farbgruppen benötigen drei frische, nahe aufeinanderfolgende Aufnahmen");
        confirmation.Reset();
        Observe(blue, 0); Observe(null, 150);
        check(Observe(blue, 300) == null && confirmation.Count == 1,
            "Eine Aufnahme ohne passenden Kandidaten unterbricht die Bestätigung");
        Observe(blue, 450);
        check(Observe(blue with { Point = new(130, 75) }, 600) == null && confirmation.Count == 1,
            "Ein neuer Fundpunkt erbt keine Bestätigungen vom vorherigen Objekt");
        confirmation.Reset(); Observe(blue, 0); Observe(blue, 150);
        check(Observe(blue, 1000) == null && confirmation.Count == 1, "Zu weit auseinanderliegende Aufnahmen zählen nicht zusammen");
        confirmation.Reset(); Observe(blue, 0); Observe(blue, 0);
        check(confirmation.Count == 1 && Observe(blue, 150) == null, "Derselbe Aufnahmezeitpunkt zählt nicht doppelt");
        confirmation.Reset(); Observe(blue, 0); Observe(blue, 150);
        check(Observe(blue with { Features = (BobberFeatures.Red | BobberFeatures.Body) }, 300) == null && confirmation.Count == 1,
            "Wechselnde einzelne Federfarben gelten nicht als stabiler Zweifarben-Treffer");
        confirmation.Reset(); Observe(blue, 0); Observe(blue, 150);
        check(Observe(blue with { Scale = 2 }, 300) == null && confirmation.Count == 1,
            "Stark wechselnde Objektgrößen starten die Bestätigung neu");
        confirmation.Reset(); Observe(blue, 0); Observe(blue, 150); confirmation.Reset();
        check(Observe(blue, 300) == null && confirmation.Count == 1,
            "Griffbearbeitung, Suchbereichswechsel und neuer Wurf können alte Bestätigungen vollständig verwerfen");
        check(Observe(blue with { Features = (BobberFeatures.Blue | BobberFeatures.Red | BobberFeatures.Body) }, 450) != null && confirmation.Count == 0,
            "Zwei passende Federn und Körper werden unmittelbar akzeptiert");
        check(Observe(blue with { Features = BobberFeatures.Body }, 600) == null,
            "Ein einzelnes Merkmal kann nicht durch Wiederholung bestätigt werden");
        var redMetal = blue with { Features = BobberFeatures.Red | BobberFeatures.Metal };
        check(Observe(redMetal, 750) == null && Observe(redMetal, 900) == null && Observe(redMetal, 1050) != null,
            "Auch Rot + Metall ohne Körperfarbe erfordern drei frische Bestätigungen");
        var frame = new PixelFrame(120, 120, new int[120 * 120]);
        var pending = BobberPreview.Mark(frame, blue, confirmed: false);
        check(pending.Pixels[blue.Point.Y * frame.Width + blue.Point.X] == 0xffb640 && frame.Pixels.All(p => p == 0),
            "Unbestätigte Kandidaten sind orange und verändern das Aufnahmebild nicht");
    }
}
