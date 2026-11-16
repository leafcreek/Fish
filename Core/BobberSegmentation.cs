using System.Buffers;

namespace Fish.Core;

internal readonly record struct BobberRegion(PixelRect Bounds, PixelPoint Center, int Count, double Quality)
{
    public double Density => Count / (double)Bounds.Area;
    public bool IsBody => Count >= 12 && Bounds.Width is >= 5 and <= 130 && Bounds.Height is >= 8 and <= 150 &&
        Bounds.Width / (double)Bounds.Height is >= .3 and <= 1.8 && Density >= .2;
    public bool IsSeparateFrom(BobberRegion body) => !body.Bounds.Contains(Center) && Bounds.Intersect(body.Bounds).Area <= Bounds.Area * .6;
}

internal readonly record struct BobberRegions(List<BobberRegion> Blue, List<BobberRegion> Red, List<BobberRegion> Bodies, List<BobberRegion> Metal);

/// <summary>Color classification and connected components. Scratch buffers are returned before layout matching starts.</summary>
internal static class BobberSegmentation
{
    private const byte Blue = 1, Red = 2, Body = 4, Metal = 8, Visited = 16;

    public static BobberRegions Find(PixelFrame frame, BobberColorProfile profile, CancellationToken cancellation, int colorTolerance = 0)
    {
        int length = frame.Pixels.Length;
        var labels = ArrayPool<byte>.Shared.Rent(length);
        var quality = ArrayPool<byte>.Shared.Rent(length);
        var supported = ArrayPool<byte>.Shared.Rent(length);
        var queue = ArrayPool<int>.Shared.Rent(length);
        try
        {
            Classify(frame, profile, labels, quality, cancellation, colorTolerance);
            SupportFeathers(frame, labels, supported, cancellation);
            var blues = Components(frame, supported, quality, queue, Blue, cancellation);
            var reds = Components(frame, supported, quality, queue, Red, cancellation);
            var metals = Components(frame, supported, quality, queue, Metal, cancellation);
            var bodies = Components(frame, labels, quality, queue, Body, cancellation);
            return new(blues, reds, bodies, metals);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(labels); ArrayPool<byte>.Shared.Return(quality);
            ArrayPool<byte>.Shared.Return(supported); ArrayPool<int>.Shared.Return(queue);
        }
    }

    private static void Classify(PixelFrame frame, BobberColorProfile profile, byte[] labels, byte[] quality, CancellationToken cancellation, int colorTolerance)
    {
        var blueMatcher = new BobberColorMatcher(profile.Blue, colorTolerance);
        var redMatcher = new BobberColorMatcher(profile.Red, colorTolerance);
        var bodyMatcher = new BobberColorMatcher(profile.Body, colorTolerance);
        var metalMatcher = new BobberColorMatcher(profile.Metal, colorTolerance);
        for (int y = 0; y < frame.Height; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            int end = (y + 1) * frame.Width;
            for (int i = y * frame.Width; i < end; i++)
            {
                int pixel = frame.Pixels[i];
                var color = HsvColor.FromRgb(pixel);
                double metal = profile.Metal.Similarity(color);
                if (metal > 0) { labels[i] = Metal; quality[i] = (byte)Math.Round(metal * 255); continue; }
                double blue = blueMatcher.Similarity(pixel, color), red = redMatcher.Similarity(pixel, color), body = bodyMatcher.Similarity(pixel, color);
                metal = colorTolerance == 0 ? 0 : metalMatcher.Similarity(pixel, color);
                double best = Math.Max(metal, Math.Max(blue, Math.Max(red, body)));
                labels[i] = best == 0 ? (byte)0 : best == metal ? Metal : best == blue ? Blue : best == red ? Red : Body;
                quality[i] = (byte)Math.Round(best * 255);
            }
        }
    }

    private static void SupportFeathers(PixelFrame frame, byte[] labels, byte[] supported, CancellationToken cancellation)
    {
        for (int y = 0; y < frame.Height; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x < frame.Width; x++)
            {
                int index = y * frame.Width + x;
                byte label = labels[index];
                supported[index] = label is Blue or Red or Metal && HasNeighbours(frame, labels, x, y, label) ? label : (byte)0;
            }
        }
    }

    private static bool HasNeighbours(PixelFrame frame, byte[] labels, int x, int y, byte label)
    {
        int count = 0, minX = Math.Max(0, x - 1), maxX = Math.Min(frame.Width - 1, x + 1), maxY = Math.Min(frame.Height - 1, y + 1);
        for (int ny = Math.Max(0, y - 1); ny <= maxY; ny++)
        for (int nx = minX; nx <= maxX; nx++)
            if ((nx != x || ny != y) && labels[ny * frame.Width + nx] == label && ++count == 2) return true;
        return false;
    }

    private static List<BobberRegion> Components(PixelFrame frame, byte[] labels, byte[] quality, int[] queue, byte color, CancellationToken cancellation)
    {
        var result = new List<BobberRegion>();
        for (int i = 0; i < frame.Pixels.Length; i++)
        {
            if ((i & 2047) == 0) cancellation.ThrowIfCancellationRequested();
            if (labels[i] != color) continue;
            int head = 0, tail = 1, minX = frame.Width, minY = frame.Height, maxX = 0, maxY = 0;
            long xSum = 0, ySum = 0, qSum = 0;
            queue[0] = i; labels[i] = (byte)(color | Visited);
            while (head < tail)
            {
                if ((head & 2047) == 0) cancellation.ThrowIfCancellationRequested();
                int index = queue[head++], x = index % frame.Width, y = index / frame.Width;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                xSum += x; ySum += y; qSum += quality[index];
                int firstX = Math.Max(0, x - 1), lastX = Math.Min(frame.Width - 1, x + 1);
                int lastRow = Math.Min(frame.Height - 1, y + 1) * frame.Width;
                for (int row = Math.Max(0, y - 1) * frame.Width; row <= lastRow; row += frame.Width)
                for (int next = row + firstX; next <= row + lastX; next++)
                {
                    if (labels[next] != color) continue;
                    labels[next] = (byte)(color | Visited); queue[tail++] = next;
                }
            }
            if (tail < (color == Body ? 12 : 4)) continue;
            var region = new BobberRegion(new(minX, minY, maxX - minX + 1, maxY - minY + 1),
                new((int)Math.Round(xSum / (double)tail), (int)Math.Round(ySum / (double)tail)), tail, qSum / (255.0 * tail));
            if (color == Metal && !HasMetalOpening(frame, labels, queue, region)) continue;
            if (color != Body || region.IsBody) result.Add(region);
        }
        return result;
    }

    private static bool HasMetalOpening(PixelFrame frame, byte[] labels, int[] queue, BobberRegion region)
    {
        var b = region.Bounds;
        if (b.Width is < 3 or > 80 || b.Height is < 4 or > 100 || region.Density > .85) return false;
        // Flood the background through four-neighbor gaps. A metal eye has an enclosed dark opening
        // and a short stem below it; filled particles, open cursor arcs and ordinary letters fail.
        const byte exterior = 32;
        int head = 0, tail = 0;
        bool IsMetal(int index) => (labels[index] & 15) == Metal;
        void Enqueue(int x, int y)
        {
            int index = y * frame.Width + x;
            if (IsMetal(index) || (labels[index] & exterior) != 0) return;
            labels[index] |= exterior; queue[tail++] = index;
        }
        for (int x = b.X; x < b.X + b.Width; x++) { Enqueue(x, b.Y); Enqueue(x, b.Y + b.Height - 1); }
        for (int y = b.Y; y < b.Y + b.Height; y++) { Enqueue(b.X, y); Enqueue(b.X + b.Width - 1, y); }
        while (head < tail)
        {
            int index = queue[head++], x = index % frame.Width, y = index / frame.Width;
            if (x > b.X) Enqueue(x - 1, y);
            if (x < b.X + b.Width - 1) Enqueue(x + 1, y);
            if (y > b.Y) Enqueue(x, y - 1);
            if (y < b.Y + b.Height - 1) Enqueue(x, y + 1);
        }
        int lastHoleY = -1;
        for (int y = b.Y + 1; y < b.Y + b.Height - 1; y++)
        for (int x = b.X + 1; x < b.X + b.Width - 1; x++)
        {
            int index = y * frame.Width + x;
            if (!IsMetal(index) && (labels[index] & exterior) == 0) lastHoleY = Math.Max(lastHoleY, y);
        }
        for (int y = b.Y; y < b.Y + b.Height; y++)
        for (int x = b.X; x < b.X + b.Width; x++) labels[y * frame.Width + x] &= unchecked((byte)~exterior);
        return lastHoleY >= 0 && b.Y + b.Height - 1 - lastHoleY >= Math.Max(2, (int)Math.Ceiling(b.Height * .18));
    }
}
