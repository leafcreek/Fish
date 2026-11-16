using Fish.Configuration;
using Fish.Core;

static class RegionEditTests
{
    public static void Run(Action<bool, string> check)
    {
        var client = new PixelRect(-1920, -200, 1920, 1080);
        var area = new PixelRect(-1400, 0, 800, 400);
        var move = new RegionEdit(area, client, new(20, 30), RegionEditKind.Move);
        check(move.At(new(120, 80)) == area with { X = -1300, Y = 50 }, "Mausbewegung verschiebt den Bereich ohne Größenänderung");
        check(move.At(new(-9999, -9999)) == area with { X = client.X, Y = client.Y }, "Verschieben begrenzt die linke und obere Kante auf die Spielfläche");
        check(move.At(new(9999, 9999)) == area with { X = -800, Y = 480 }, "Verschieben begrenzt die rechte und untere Kante auf die Spielfläche");
        var resize = move with { Kind = RegionEditKind.Resize };
        check(resize.At(new(120, 80)) == area with { Width = 900, Height = 450 }, "Eckgriff ändert Breite und Höhe bei festem Ursprung");
        check(resize.At(new(-9999, -9999)) == area with { Width = 100, Height = 100 }, "Skalierung hält die Mindestgröße von 100 Pixeln ein");
        check(resize.At(new(9999, 9999)) == area with { Width = 1400, Height = 880 }, "Skalierung endet an der rechten und unteren Spielkante");
        var huge = new RegionEdit(new(0, 0, 1000, 800), new(0, 0, 7680, 4320), new(0, 0), RegionEditKind.Resize);
        check(huge.At(new(9999, 9999)) is { Width: 3840, Height: 2160 }, "Große Monitore ändern die unterstützten maximalen Suchgrößen nicht");
        foreach (var rect in new[] { area, area with { Width = 801, Height = 401 }, new PixelRect(-1920, -200, 100, 101) })
        {
            SearchRegion region = RegionEdit.ToRegion(rect, client);
            check(region.In(client) == rect, "Mausposition lässt sich auch bei ungeraden Größen und negativen Monitoren verlustfrei speichern");
        }
        check(RegionEdit.ToRegion(area with { Y = -50 }, client).YOffset > RegionEdit.ToRegion(area, client).YOffset,
            "Verschieben nach oben erhöht den gespeicherten Y-Versatz");
    }
}
