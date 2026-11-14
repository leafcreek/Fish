using System.Buffers;

namespace Fish.Core;

public sealed record BobberMatch(PixelRect Bounds, PixelPoint Point, double Confidence, double Scale,
    double RotationDegrees = 0, double AspectRatio = 1, double Perspective = 0);

/// <summary>Masked projective matching with global image-pyramid search and local pose refinement.</summary>
public sealed class BobberTemplateMatcher
{
    private const int SeedLimit = 12;
    private readonly BobberTemplate _template;
    private readonly Seed[] _seeds;
    private readonly BodySample[] _bodySamples;
    private readonly double _bodyRedGreen, _bodyGreenBlue, _bodyChroma;
    private readonly record struct BodySample(int X, int Y, double Weight);
    private readonly record struct Sample(int X, int Y, double Value, double Weight);
    private sealed record Pattern(Sample[] Samples, double Energy, double Weight, PixelRect ActiveBounds);
    private sealed record Variant(BobberProjection Geometry, Pattern Pattern);
    private sealed record Seed(BobberPose Pose, Variant HalfSize);
    private readonly record struct Candidate(BobberPose Pose, PixelPoint Point, double Score);
    private sealed record Refinement(Variant Variant, int X, int Y, double Score);

    public BobberTemplateMatcher(BobberTemplate template)
    {
        Validate(template.Image);
        if (template.Anchor.X < 0 || template.Anchor.X >= template.Image.Width ||
            template.Anchor.Y < 0 || template.Anchor.Y >= template.Image.Height)
            throw new ArgumentException("Der Bobber-Anker liegt außerhalb der Bildvorlage.");
        _template = template;
        var body = new List<BodySample>();
        double bodyWeight = 0, redGreen = 0, greenBlue = 0;
        int radiusX = Math.Max(1, (int)Math.Round(template.Image.Width * .1));
        int radiusY = Math.Max(1, (int)Math.Round(template.Image.Height * .07));
        foreach (int dy in new[] { -radiusY, 0, radiusY })
        foreach (int dx in new[] { -radiusX, 0, radiusX })
        {
            int x = template.Anchor.X + dx, y = template.Anchor.Y + dy;
            if (x < 0 || y < 0 || x >= template.Image.Width || y >= template.Image.Height) continue;
            int pixel = template.Image.Pixels[y * template.Image.Width + x];
            double weight = template.UseAlpha ? ((uint)pixel >> 24) / 255.0 : 1;
            if (weight == 0) continue;
            body.Add(new(x, y, weight)); bodyWeight += weight;
            redGreen += weight * (((pixel >> 16) & 255) - ((pixel >> 8) & 255));
            greenBlue += weight * (((pixel >> 8) & 255) - (pixel & 255));
        }
        _bodySamples = body.ToArray();
        _bodyRedGreen = bodyWeight == 0 ? 0 : redGreen / bodyWeight;
        _bodyGreenBlue = bodyWeight == 0 ? 0 : greenBlue / bodyWeight;
        _bodyChroma = Math.Sqrt(_bodyRedGreen * _bodyRedGreen + _bodyGreenBlue * _bodyGreenBlue);
        var seeds = new List<Seed>();
        foreach (double scale in new[] { .6, .8, 1, 1.2, 1.4 })
        foreach (double angle in new[] { -30.0, 0, 30 })
        foreach (double aspect in new[] { .8, 1, 1.2 })
        foreach (double perspective in new[] { -.18, 0, .18 })
        {
            var pose = new BobberPose(scale, angle, aspect, perspective);
            var variant = Prepare(pose with { Scale = scale / 2 }, 6, 8);
            if (variant.Pattern.Energy >= 1) seeds.Add(new(pose, variant));
        }
        if (seeds.Count == 0) throw new ArgumentException("Die sichtbaren Bereiche der Bildvorlage enthalten zu wenig Struktur.");
        _seeds = seeds.ToArray();
    }

    public BobberMatch? Find(PixelFrame frame, int minimumPercent, CancellationToken cancellation = default)
    {
        Validate(frame);
        if (minimumPercent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(minimumPercent));
        cancellation.ThrowIfCancellationRequested();
        var gray = ArrayPool<byte>.Shared.Rent(frame.Pixels.Length);
        int halfWidth = (frame.Width + 1) / 2, halfHeight = (frame.Height + 1) / 2;
        var half = ArrayPool<byte>.Shared.Rent(halfWidth * halfHeight);
        try
        {
            for (int i = 0; i < frame.Pixels.Length; i++) gray[i] = Luminance(frame.Pixels[i]);
            for (int y = 0; y < halfHeight; y++)
            for (int x = 0; x < halfWidth; x++)
            {
                int x0 = x * 2, y0 = y * 2, x1 = Math.Min(x0 + 1, frame.Width - 1), y1 = Math.Min(y0 + 1, frame.Height - 1);
                half[y * halfWidth + x] = (byte)((gray[y0 * frame.Width + x0] + gray[y0 * frame.Width + x1] +
                    gray[y1 * frame.Width + x0] + gray[y1 * frame.Width + x1] + 2) / 4);
            }
            var candidates = FindSeeds(half, halfWidth, halfHeight, gray, frame.Pixels, frame.Width, frame.Height, cancellation);
            BobberMatch? best = null;
            foreach (var candidate in candidates)
            foreach (var refinement in Refine(candidate, gray, frame.Pixels, frame.Width, frame.Height, cancellation))
            {
                var pose = refinement.Variant.Geometry.Pose;
                var dense = Prepare(pose, 24, 32);
                for (int y = refinement.Y - 1; y <= refinement.Y + 1; y++)
                for (int x = refinement.X - 1; x <= refinement.X + 1; x++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!Fits(dense, x, y, frame.Width, frame.Height)) continue;
                    double score = Appearance(frame.Pixels, frame.Width, frame.Height, x, y, dense,
                        Score(gray, frame.Width, x, y, dense.Pattern));
                    if (score > -1 && (best == null || score > best.Confidence))
                        best = ToMatch(dense, x, y, score, frame.Width, frame.Height);
                }
            }
            if (best != null) best = RefineBest(best, gray, frame.Pixels, frame.Width, frame.Height, cancellation);
            return best?.Confidence >= minimumPercent / 100.0 ? best : null;
        }
        finally { ArrayPool<byte>.Shared.Return(gray); ArrayPool<byte>.Shared.Return(half); }
    }

    private List<Candidate> FindSeeds(byte[] image, int width, int height, byte[] full, int[] colors, int fullWidth, int fullHeight, CancellationToken cancellation)
    {
        var candidates = new List<Candidate>(SeedLimit);
        foreach (var seed in _seeds)
        {
            // Preserve candidates per pose until they have been checked at original resolution.
            // Otherwise an aliased small/wide seed can suppress the correct pose of the same bobber.
            var poseCandidates = new List<Candidate>(2);
            var variant = seed.HalfSize;
            var active = variant.Pattern.ActiveBounds;
            int minX = -active.X, minY = -active.Y;
            int maxX = width - active.X - active.Width, maxY = height - active.Y - active.Height;
            if (maxX < minX || maxY < minY) continue;
            for (int y = minY; ; y = Math.Min(y + 2, maxY))
            {
                cancellation.ThrowIfCancellationRequested();
                for (int x = minX; ; x = Math.Min(x + 2, maxX))
                {
                    if (!Fits(variant, x, y, width, height)) { if (x == maxX) break; else continue; }
                    double score = Score(image, width, x, y, variant.Pattern);
                    var point = new PixelPoint(2 * (x + variant.Geometry.Anchor.X), 2 * (y + variant.Geometry.Anchor.Y));
                    if (score > -1) KeepSeed(poseCandidates, new(seed.Pose, point, score), 2);
                    if (x == maxX) break;
                }
                if (y == maxY) break;
            }
            var fullVariant = Prepare(seed.Pose, 12, 16);
            var denseVariant = Prepare(seed.Pose, 24, 32);
            foreach (var candidate in poseCandidates)
            {
                int centerX = candidate.Point.X - fullVariant.Geometry.Anchor.X;
                int centerY = candidate.Point.Y - fullVariant.Geometry.Anchor.Y;
                Candidate? verified = null;
                for (int y = centerY - 12; y <= centerY + 12; y++)
                for (int x = centerX - 12; x <= centerX + 12; x++)
                {
                    if (!Fits(fullVariant, x, y, fullWidth, fullHeight)) continue;
                    double score = Score(full, fullWidth, x, y, fullVariant.Pattern);
                    if (verified == null || score > verified.Value.Score)
                        verified = new(seed.Pose, new(x + fullVariant.Geometry.Anchor.X, y + fullVariant.Geometry.Anchor.Y), score);
                }
                if (verified is not { } hit) continue;
                // Compare poses with the same dense metric before spatial suppression; sparse grids can rank them differently.
                Candidate? denseHit = null;
                centerX = hit.Point.X - denseVariant.Geometry.Anchor.X;
                centerY = hit.Point.Y - denseVariant.Geometry.Anchor.Y;
                for (int y = centerY - 2; y <= centerY + 2; y++)
                for (int x = centerX - 2; x <= centerX + 2; x++)
                {
                    if (!Fits(denseVariant, x, y, fullWidth, fullHeight)) continue;
                    double score = Appearance(colors, fullWidth, fullHeight, x, y, denseVariant,
                        Score(full, fullWidth, x, y, denseVariant.Pattern));
                    if (denseHit == null || score > denseHit.Value.Score)
                        denseHit = new(seed.Pose, new(x + denseVariant.Geometry.Anchor.X, y + denseVariant.Geometry.Anchor.Y), score);
                }
                if (denseHit is { } selected) KeepSeed(candidates, selected, SeedLimit);
            }
        }
        return candidates;
    }

    private static void KeepSeed(List<Candidate> candidates, Candidate candidate, int limit)
    {
        // A moving maximum may overlap several earlier maxima. Remove every weaker neighbour to avoid duplicates.
        foreach (var existing in candidates)
            if (Math.Abs(existing.Point.X - candidate.Point.X) < 12 && Math.Abs(existing.Point.Y - candidate.Point.Y) < 12 &&
                existing.Score >= candidate.Score) return;
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (Math.Abs(candidates[i].Point.X - candidate.Point.X) < 12 &&
                Math.Abs(candidates[i].Point.Y - candidate.Point.Y) < 12)
            {
                candidates.RemoveAt(i);
            }
        }
        if (candidates.Count < limit) { candidates.Add(candidate); return; }
        int worst = 0;
        for (int i = 1; i < candidates.Count; i++) if (candidates[i].Score < candidates[worst].Score) worst = i;
        if (candidate.Score > candidates[worst].Score) candidates[worst] = candidate;
    }

    private List<Refinement> Refine(Candidate candidate, byte[] gray, int[] colors, int width, int height, CancellationToken cancellation)
    {
        var best = new List<Refinement>(4);
        foreach (double scale in Nearby(candidate.Pose.Scale, .1, .6, 1.4))
        foreach (double angle in Nearby(candidate.Pose.RotationDegrees, 15, -30, 30))
        foreach (double aspect in Nearby(candidate.Pose.AspectRatio, .1, .8, 1.2))
        foreach (double perspective in Nearby(candidate.Pose.Perspective, .09, -.18, .18))
        {
            cancellation.ThrowIfCancellationRequested();
            var variant = Prepare(new(scale, angle, aspect, perspective), 12, 16);
            if (variant.Pattern.Energy < 1) continue;
            int centerX = candidate.Point.X - variant.Geometry.Anchor.X, centerY = candidate.Point.Y - variant.Geometry.Anchor.Y;
            Refinement? local = null;
            for (int y = centerY - 12; y <= centerY + 12; y += 2)
            for (int x = centerX - 12; x <= centerX + 12; x += 2)
            {
                if (!Fits(variant, x, y, width, height)) continue;
                double score = Appearance(colors, width, height, x, y, variant, Score(gray, width, x, y, variant.Pattern));
                if (local == null || score > local.Score) local = new(variant, x, y, score);
            }
            if (local == null) continue;
            int worst = 0;
            for (int i = 1; i < best.Count; i++) if (best[i].Score < best[worst].Score) worst = i;
            if (best.Count < 4) best.Add(local);
            else if (local.Score > best[worst].Score) best[worst] = local;
        }
        return best;
    }

    private static IEnumerable<double> Nearby(double value, double step, double min, double max)
        => new[] { value - step, value, value + step }.Select(v => Math.Round(Math.Clamp(v, min, max), 3)).Distinct();

    private BobberMatch RefineBest(BobberMatch initial, byte[] gray, int[] colors, int width, int height, CancellationToken cancellation)
    {
        var best = initial;
        foreach (double scale in Nearby(initial.Scale, .05, .6, 1.4))
        foreach (double angle in Nearby(initial.RotationDegrees, 7.5, -30, 30))
        foreach (double aspect in Nearby(initial.AspectRatio, .05, .8, 1.2))
        foreach (double perspective in Nearby(initial.Perspective, .045, -.18, .18))
        {
            cancellation.ThrowIfCancellationRequested();
            var variant = Prepare(new(scale, angle, aspect, perspective), 24, 32);
            int centerX = initial.Point.X - variant.Geometry.Anchor.X, centerY = initial.Point.Y - variant.Geometry.Anchor.Y;
            for (int y = centerY - 6; y <= centerY + 6; y++)
            for (int x = centerX - 6; x <= centerX + 6; x++)
            {
                if (!Fits(variant, x, y, width, height)) continue;
                double score = Appearance(colors, width, height, x, y, variant, Score(gray, width, x, y, variant.Pattern));
                if (score > best.Confidence) best = ToMatch(variant, x, y, score, width, height);
            }
        }
        return best;
    }

    private static BobberMatch ToMatch(Variant variant, int x, int y, double score, int width, int height)
    {
        var geometry = variant.Geometry;
        return new(new PixelRect(x, y, geometry.Width, geometry.Height).Intersect(new(0, 0, width, height)),
            new(x + geometry.Anchor.X, y + geometry.Anchor.Y), score, geometry.Pose.Scale,
            geometry.Pose.RotationDegrees, geometry.Pose.AspectRatio, geometry.Pose.Perspective);
    }

    private double Appearance(int[] pixels, int width, int height, int x, int y, Variant variant, double correlation)
    {
        if (correlation <= 0 || _bodyChroma < 3) return correlation;
        double redGreen = 0, greenBlue = 0, weight = 0;
        foreach (var sample in _bodySamples)
        {
            var point = variant.Geometry.Output(sample.X, sample.Y);
            int sx = x + (int)Math.Round(point.X), sy = y + (int)Math.Round(point.Y);
            if (sx < 0 || sy < 0 || sx >= width || sy >= height) continue;
            int pixel = pixels[sy * width + sx];
            redGreen += sample.Weight * (((pixel >> 16) & 255) - ((pixel >> 8) & 255));
            greenBlue += sample.Weight * (((pixel >> 8) & 255) - (pixel & 255));
            weight += sample.Weight;
        }
        if (weight == 0) return 0;
        redGreen /= weight; greenBlue /= weight;
        double chroma = Math.Sqrt(redGreen * redGreen + greenBlue * greenBlue);
        if (chroma < 1e-6) return 0;
        // Channel differences survive uniform brightness changes; direction separates the brown body from pink/grey water.
        double direction = Math.Clamp((redGreen * _bodyRedGreen + greenBlue * _bodyGreenBlue) / (chroma * _bodyChroma), 0, 1);
        return correlation * direction * direction * Math.Min(1, chroma / (_bodyChroma * .35));
    }

    private static bool Fits(Variant variant, int x, int y, int width, int height)
    {
        var active = variant.Pattern.ActiveBounds;
        return x + active.X >= 0 && y + active.Y >= 0 && x + active.X + active.Width <= width &&
            y + active.Y + active.Height <= height && x + variant.Geometry.Anchor.X >= 0 &&
            y + variant.Geometry.Anchor.Y >= 0 && x + variant.Geometry.Anchor.X < width && y + variant.Geometry.Anchor.Y < height;
    }

    public static PixelFrame Mark(PixelFrame frame, BobberMatch match)
    {
        var pixels = (int[])frame.Pixels.Clone();
        var rect = match.Bounds;
        const int color = 0x00ff80;
        for (int x = rect.X; x < rect.X + rect.Width; x++)
        {
            pixels[rect.Y * frame.Width + x] = color;
            pixels[(rect.Y + rect.Height - 1) * frame.Width + x] = color;
        }
        for (int y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            pixels[y * frame.Width + rect.X] = color;
            pixels[y * frame.Width + rect.X + rect.Width - 1] = color;
        }
        for (int offset = -4; offset <= 4; offset++)
        {
            int x = match.Point.X + offset, y = match.Point.Y + offset;
            if (x >= 0 && x < frame.Width) pixels[match.Point.Y * frame.Width + x] = color;
            if (y >= 0 && y < frame.Height) pixels[y * frame.Width + match.Point.X] = color;
        }
        return new(frame.Width, frame.Height, pixels);
    }

    private static double Score(byte[] image, int stride, int x, int y, Pattern pattern)
    {
        if (pattern.Energy < 1 || pattern.Weight == 0) return -1;
        double sum = 0, squares = 0, product = 0;
        foreach (var sample in pattern.Samples)
        {
            double value = image[(y + sample.Y) * stride + x + sample.X];
            sum += sample.Weight * value; squares += sample.Weight * value * value; product += value * sample.Value;
        }
        double energy = squares - sum * sum / pattern.Weight;
        return energy < pattern.Weight * 9 ? -1 : Math.Clamp(product / Math.Sqrt(energy * pattern.Energy), -1, 1);
    }

    private Variant Prepare(BobberPose pose, int columns, int rows)
    {
        var geometry = new BobberProjection(_template, pose);
        columns = Math.Min(columns, geometry.Width); rows = Math.Min(rows, geometry.Height);
        var visible = new List<Sample>(columns * rows);
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            int x = (int)Math.Round(column * (geometry.Width - 1.0) / (columns - 1));
            int y = (int)Math.Round(row * (geometry.Height - 1.0) / (rows - 1));
            var source = geometry.Source(x, y);
            var pixel = SampleTemplate(source.X, source.Y);
            if (pixel.Weight > 0) visible.Add(new(x, y, pixel.Value, pixel.Weight));
        }
        var samples = visible.ToArray();
        double weight = samples.Sum(s => s.Weight);
        double mean = weight == 0 ? 0 : samples.Sum(s => s.Value * s.Weight) / weight, energy = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double centered = samples[i].Value - mean;
            energy += centered * centered * samples[i].Weight;
            samples[i] = samples[i] with { Value = centered * samples[i].Weight };
        }
        var active = samples.Length == 0 ? default : new PixelRect(samples.Min(s => s.X), samples.Min(s => s.Y),
            samples.Max(s => s.X) - samples.Min(s => s.X) + 1, samples.Max(s => s.Y) - samples.Min(s => s.Y) + 1);
        return new(geometry, new(samples, energy, weight, active));
    }

    private (double Value, double Weight) SampleTemplate(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return (0, 0);
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double value = 0, weight = 0, fx = x - x0, fy = y - y0;
        for (int dy = 0; dy <= 1; dy++)
        for (int dx = 0; dx <= 1; dx++)
        {
            int sx = x0 + dx, sy = y0 + dy;
            if (sx < 0 || sy < 0 || sx >= _template.Image.Width || sy >= _template.Image.Height) continue;
            int pixel = _template.Image.Pixels[sy * _template.Image.Width + sx];
            double alpha = _template.UseAlpha ? ((uint)pixel >> 24) / 255.0 : 1;
            double contribution = (dx == 0 ? 1 - fx : fx) * (dy == 0 ? 1 - fy : fy) * alpha;
            value += Luminance(pixel) * contribution; weight += contribution;
        }
        return weight == 0 ? (0, 0) : (value / weight, weight);
    }

    private static byte Luminance(int pixel) => (byte)((77 * ((pixel >> 16) & 255) +
        150 * ((pixel >> 8) & 255) + 29 * (pixel & 255)) >> 8);

    private static void Validate(PixelFrame frame)
    {
        if (frame.Width < 2 || frame.Height < 2 || frame.Pixels.Length != checked(frame.Width * frame.Height))
            throw new ArgumentException("Ungültiger Bildpuffer für Template Matching.");
    }
}
