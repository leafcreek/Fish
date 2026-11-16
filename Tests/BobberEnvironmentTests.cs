using Fish.Configuration;
using Fish.Core;

internal static class BobberEnvironmentTests
{
    public static void Run(Action<bool, string> check)
    {
        var detector = new BobberDetector();
        using var input = typeof(BobberEnvironmentTests).Assembly.GetManifestResourceStream("Fish.Tests.forever-dark-sea.png")!;
        var screenshot = TestBitmapDecoder.Read(input);
        var night = Crop(screenshot, new(188, 110, 721, 283));
        var body = new PixelRect(159, 86, 44, 47);
        int visible = 0;
        for (int level = 0; level <= 100; level++)
        {
            var tuning = DetectionSensitivity.Bobber(new() { BobberSensitivity = level });
            var match = detector.Find(night, tuning.MinimumRating, colorTolerance: tuning.ColorTolerance);
            if (match != null && !body.Contains(match.Point)) throw new Exception($"Falscher Nacht-Klickpunkt bei Regler {level}: {match}");
            if (match != null) visible++;
            if (level >= 50 && match == null) throw new Exception($"Nacht-Schwimmer fehlt bei Regler {level}.");
        }
        check(visible >= 51, $"Dunkles Meer: {visible}/101 Reglerstellungen mit korrektem Klickpunkt; keine falsche Stelle");

        var waters = new[] { 0x0A1928, 0x194F70, 0x28594C, 0x666C70, 0x6E402C, 0xAF9673 };
        var tints = new[] { (1d, 1d, 1d), (.7, .95, 1.25), (1.25, .95, .7), (.85, 1.1, .85) };
        int positives = 0, negatives = 0;
        var failed = new List<string>();
        foreach (int water in waters)
        foreach (double power in new[] { .35, .75, 1.5 })
        foreach (var tint in tints)
        foreach (double waves in new[] { 1d, 2.5 })
        foreach (double phase in new[] { 0d, .7, 1.4 })
        {
            var light = (power, tint.Item1, tint.Item2, tint.Item3);
            var scene = Water(water, phase, waves);
            // Light changes also affect the water, independently of its underlying color.
            scene = Relight(scene, light);
            foreach (var setting in new[] { (50, 50), (1, 90) })
            {
                var empty = detector.Find(scene, setting.Item1, colorTolerance: setting.Item2);
                if (empty != null) failed.Add($"Wasser ohne Schwimmer {water:X6}/{light}/{waves}/{phase}: {empty}");
                negatives++;
            }
            var bobber = Relight(ReferenceBobber.Default.Image, light);
            BobberTestImages.Paste(scene, bobber, 115, 72);
            var found = detector.Find(scene, 50, colorTolerance: 50);
            if (found == null || !new PixelRect(168, 115, 38, 45).Contains(found.Point))
                failed.Add($"Schwimmer {water:X6}/{light}/{waves}/{phase}: {found}");
            positives++;
        }
        if (failed.Count != 0) throw new Exception($"{failed.Count} Umgebungsfehler:\n" + string.Join("\n", failed.Take(20)));
        check(true, $"{positives} Kombinationen aus Wasserfarbe, Helligkeit, kaltem/warmem/grünem Licht und bewegten Wellen korrekt gefunden");
        check(true, $"{negatives} entsprechende Wasserbilder ohne Schwimmer bleiben bei mittlerer und maximaler Toleranz ohne Treffer");

        foreach (string name in new[] { "forever-water.png", "forever-water-cursor.png", "forever-shore.png" })
        {
            using var source = typeof(BobberEnvironmentTests).Assembly.GetManifestResourceStream("Fish.Tests." + name)!;
            var frame = TestBitmapDecoder.Read(source);
            foreach (int level in new[] { 0, 50, 100 })
            {
                var tuning = DetectionSensitivity.Bobber(new() { BobberSensitivity = level });
                check(detector.Find(frame, tuning.MinimumRating, colorTolerance: tuning.ColorTolerance) == null,
                    $"Echter Negativfall {name} ohne Treffer bei Regler {level}");
            }
        }
    }

    private static PixelFrame Crop(PixelFrame frame, PixelRect area)
    {
        var pixels = new int[area.Area];
        for (int y=0; y<area.Height; y++) Array.Copy(frame.Pixels,(area.Y+y)*frame.Width+area.X,pixels,y*area.Width,area.Width);
        return new(area.Width,area.Height,pixels);
    }
    private static PixelFrame Water(int color, double phase, double strength)
    {
        const int width=320, height=230;
        var pixels = new int[width*height];
        for(int y=0;y<height;y++)
        for(int x=0;x<width;x++)
        {
            double wave=strength*(9*Math.Sin(y*.4+x*.017+phase)+5*Math.Sin(y*.19-x*.045+phase));
            double reflection=Math.Pow(Math.Max(0,Math.Sin(y*.71+x*.025+phase)),16)*22*strength;
            int pixel=0;
            for(int shift=0;shift<=16;shift+=8) pixel|=Math.Clamp((int)Math.Round(((color>>shift)&255)+wave+reflection),0,255)<<shift;
            pixels[y*width+x]=pixel;
        }
        return new(width,height,pixels);
    }
    private static PixelFrame Relight(PixelFrame frame, (double Power,double R,double G,double B) light)
    {
        int Channel(int pixel,int shift,double gain) => Math.Clamp((int)Math.Round(((pixel>>shift)&255)*light.Power*gain),0,255);
        var pixels=frame.Pixels.Select(p=>(p&unchecked((int)0xff000000))|Channel(p,16,light.R)<<16|Channel(p,8,light.G)<<8|Channel(p,0,light.B)).ToArray();
        return new(frame.Width,frame.Height,pixels);
    }
}
