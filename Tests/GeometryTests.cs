using Fish.Core;

static class GeometryTests
{
    public static void Run(Action<bool, string> check)
    {
        var monitor = new MonitorInfo("TEST", new(-1920, -200, 1920, 1080));
        check(PixelRect.Around(new(-960, 340), 180, 120, 0, 20, monitor.Bounds) == new PixelRect(-1050, 260, 180, 120), "Negative Monitorkoordinaten und Y-Versatz");
        check(PixelRect.Around(new(-1910, -190), 100, 100, 0, 0, monitor.Bounds) == new PixelRect(-1920, -200, 60, 60), "Suchbereich am Monitorrand beschneiden");

        var mainWork = new PixelRect(0, 0, 1920, 1040);
        var leftWork = new PixelRect(-1920, -100, 1920, 1040);
        check(WindowPlacement.Fit(new(-1800, -50, 760, 740), [mainWork, leftWork], mainWork) == new PixelRect(-1800, -50, 760, 740), "Fensterposition auf Monitor mit negativen Koordinaten bleibt erhalten");
        var recovered = WindowPlacement.Fit(new(6000, 200, 760, 740), [mainWork], mainWork);
        check(recovered == new PixelRect(580, 150, 760, 740), "Entfernter Monitor führt zu sichtbarem zentriertem Fenster");
        check(WindowPlacement.Fit(new(0, 0, 3000, 2000), [mainWork], mainWork) == mainWork, "Übergroßes Fenster wird auf die Arbeitsfläche ohne Taskleiste begrenzt");
        check(WindowPlacement.Fit(new(0, 0, 10, 10), [mainWork], mainWork) == new PixelRect(0, 0, 400, 360),
            "Kompakte Mindestgröße verhindert abgeschnittene Bedienelemente");
        check(WindowPlacement.Fit(new(0, 0, 10, 10), [mainWork], mainWork, 600, 540) == new PixelRect(0, 0, 600, 540),
            "Mindestgröße kann passend zur Windows-Skalierung erhöht werden");
        check(new PixelPoint(-1920, -200).DistanceSquaredTo(new(-1917, -196)) == 25,
            "Abstandsprüfung funktioniert auch mit negativen Bildschirmkoordinaten");
        var region = new PixelRect(-100, -50, 80, 60);
        check(region.Contains(new PixelRect(-90, -40, 20, 10)) && !region.Contains(new PixelRect(-110, -40, 20, 10)),
            "Bereiche werden nur bei vollständig enthaltenen Grenzen als enthalten erkannt");
        check(region.Union(new(-30, -10, 40, 30)) == new PixelRect(-100, -50, 110, 70) && region.Union(default) == region,
            "Vereinigung berücksichtigt überlappende und leere Bereiche ohne Koordinatenverlust");
    }
}
