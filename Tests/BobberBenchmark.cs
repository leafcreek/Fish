using Fish.Core;
using System.Diagnostics;
using System.Text.Json;

static class BobberBenchmark
{
    private sealed record MatchResult(int X, int Y, int Width, int Height, int PointX, int PointY,
        double Confidence, double Scale, string Features);
    private sealed record Result(string Name, MatchResult? At10, MatchResult? At80, double Milliseconds, long AllocatedBytes);

    public static void Run(string output, string? comparison)
    {
        var detector = new BobberDetector();
        MatchResult? Find(PixelFrame frame, int minimum)
        {
            var m = detector.Find(frame, minimum);
            return m == null ? null : new(m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height,
                m.Point.X, m.Point.Y, m.Confidence, m.Scale, m.Features.ToString());
        }
        var results = new List<Result>();
        foreach (string name in new[] { "forever-bobber-area.png", "forever-feathered-bobber.png", "forever-water.png",
            "forever-water-cursor.png", "forever-shore.png", "forever-sunset.png", "forever-sunset-area.png", "forever-night.png" })
        {
            using var stream = typeof(BobberBenchmark).Assembly.GetManifestResourceStream($"Fish.Tests.{name}")!;
            var frame = TestBitmapDecoder.Read(stream);
            var at10 = Find(frame, 10); var at80 = Find(frame, 80);
            for (int i = 0; i < 3; i++) detector.Find(frame, 80);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long timestamp = Stopwatch.GetTimestamp();
            for (int i = 0; i < 10; i++) detector.Find(frame, 80);
            double milliseconds = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds / 10;
            allocated = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 10;
            results.Add(new(name, at10, at80, milliseconds, allocated));
            Console.WriteLine($"{name}: {milliseconds:F2} ms · {allocated} Bytes/Scan");
        }
        File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        if (comparison == null) return;
        var expected = JsonSerializer.Deserialize<List<Result>>(File.ReadAllText(comparison))!;
        bool Same(MatchResult? a, MatchResult? b) => a == null || b == null ? a == b :
            a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height && a.PointX == b.PointX && a.PointY == b.PointY &&
            a.Features == b.Features && Math.Abs(a.Confidence - b.Confidence) < 1e-10 && Math.Abs(a.Scale - b.Scale) < 1e-10;
        if (expected.Count != results.Count || expected.Zip(results).Any(pair => pair.First.Name != pair.Second.Name ||
            !Same(pair.First.At10, pair.Second.At10) || !Same(pair.First.At80, pair.Second.At80)))
            throw new Exception("Bobber-Ergebnisse weichen von der Vergleichsdatei ab.");
        Console.WriteLine("PASS alle Bildtreffer, Klickpunkte, Merkmalsgruppen und Bewertungen entsprechen dem Vergleichslauf");
    }
}
