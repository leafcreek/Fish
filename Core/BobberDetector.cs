using System.Buffers;

namespace Fish.Core;

/// <summary>Finds at least two distinct, spatially consistent features from the four-part reference.</summary>
internal sealed class BobberDetector
{
    private const int MaximumCandidatesPerFeature = 96;
    private readonly BobberColorProfile _profile;
    private readonly BobberColorSignature[] _signatures;
    private readonly SearchPose[] _poses;
    private static readonly BobberFeatures[] Features = [BobberFeatures.Body, BobberFeatures.Blue, BobberFeatures.Red, BobberFeatures.Metal];
    private readonly record struct Layout(double X, double Y, double Width, double Height);
    private sealed record SearchPose(double Size, double Aspect, Layout[] Layouts);
    private readonly record struct FeatureMatch(BobberRegion Region, double ErrorSquared, double Ratio)
    {
        public bool IsStrong => ErrorSquared <= 1 && Ratio >= .2;
        public double MassError => Math.Abs(Math.Log(Ratio));
    }

    public BobberDetector(BobberColorProfile? profile = null)
    {
        _profile = profile ?? BobberColorProfile.Default;
        _signatures = [_profile.Body, _profile.Blue, _profile.Red, _profile.Metal];
        _poses = CreatePoses();
    }

    public BobberMatch? Find(PixelFrame frame, int minimumPercent, CancellationToken cancellation = default, int colorTolerance = 0)
    {
        if (frame.Width < 2 || frame.Height < 2 || frame.Pixels.Length != checked(frame.Width * frame.Height))
            throw new ArgumentException("Ungültiger Bildpuffer für die Schwimmer-Suche.");
        if (minimumPercent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(minimumPercent));
        if (colorTolerance is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(colorTolerance));
        cancellation.ThrowIfCancellationRequested();
        var mask = ArrayPool<byte>.Shared.Rent(frame.Pixels.Length);
        byte[]? structure = null, metalMask = null;
        byte[] activeMask = mask;
        try
        {
            BobberForeground.Mark(frame, mask, cancellation);
            BobberMatch? UnderLight(BobberLight? light) => FindRegions(frame,
                BobberSegmentation.Find(frame, _profile, cancellation, colorTolerance, foregroundOnly: true, foregroundMask: activeMask, light: light, metalMask: metalMask),
                minimumPercent, cancellation);
            var local = UnderLight(null);
            if (IsStrong(local)) return local;
            // Compare paths rather than accepting the first plausible pair. A tiny fragment or
            // reflection must not hide a more complete, better fitting bobber elsewhere.
            var best = Better(local, FindRegions(frame, BobberSegmentation.Find(frame, _profile, cancellation), minimumPercent, cancellation));
            if (IsStrong(best)) return best;
            foreach (var light in BobberLighting.Estimate(frame, mask, cancellation))
            {
                var corrected = UnderLight(light);
                best = Better(best, corrected);
                if (IsStrong(best)) break;
            }
            if (!IsStrong(best))
            {
                // Bright reflections can join a feather even after a luminance comparison.
                // Opponent channels remove additive brightness changes from those waves.
                BobberForeground.Mark(frame, mask, cancellation, chromaticOnly: true);
                best = Better(best, UnderLight(null));
                if (!IsStrong(best))
                    foreach (var light in BobberLighting.Estimate(frame, mask, cancellation))
                    {
                        best = Better(best, UnderLight(light));
                        if (IsStrong(best)) break;
                    }
            }
            if (best == null || best.FeatureCount < 3)
            {
                // Keep the eye's fine contour in the original mask while requiring vertical
                // support for feathers/cork, so horizontal glints cannot join those surfaces.
                BobberForeground.Mark(frame, mask, cancellation);
                structure = ArrayPool<byte>.Shared.Rent(frame.Pixels.Length);
                BobberForeground.SuppressRipples(frame, mask, structure, cancellation);
                activeMask = structure; metalMask = mask;
                best = Better(best, UnderLight(null));
                if (!IsStrong(best))
                    foreach (var light in BobberLighting.Estimate(frame, mask, cancellation))
                    {
                        best = Better(best, UnderLight(light));
                        if (IsStrong(best)) break;
                    }
            }
            return best;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(mask);
            if (structure != null) ArrayPool<byte>.Shared.Return(structure);
        }
    }

    private static bool IsStrong(BobberMatch? match) => match is { FeatureCount: >= 3, Confidence: >= .94 };
    private static double Evidence(BobberMatch match) => match.Confidence + .03 * match.FeatureCount +
        (match.Features.HasFlag(BobberFeatures.Body) ? .01 : 0);
    private static BobberMatch? Better(BobberMatch? first, BobberMatch? second) =>
        second != null && (first == null || Evidence(second) > Evidence(first)) ? second : first;

    private BobberMatch? FindRegions(PixelFrame frame, BobberRegions regions, int minimumPercent, CancellationToken cancellation)
    {
        List<BobberRegion>[] groups = [regions.Bodies, regions.Blue, regions.Red, regions.Metal];
        var bounds = new PixelRect(0, 0, frame.Width, frame.Height);
        BobberMatch? best = null;
        Span<FeatureMatch?> matches = stackalloc FeatureMatch?[4];
        for (int anchor = 0; anchor < groups.Length; anchor++)
        foreach (var region in groups[anchor].OrderByDescending(r => r.Count * r.Quality).Take(MaximumCandidatesPerFeature))
        {
            cancellation.ThrowIfCancellationRequested();
            if (anchor == 0 && region.Density < .45) continue;
            double estimated = Math.Sqrt(region.Bounds.Area / (double)_signatures[anchor].Bounds.Area);
            if (estimated is < .35 or > 2.3) continue;
            foreach (var pose in _poses)
            {
                cancellation.ThrowIfCancellationRequested();
                double scale = estimated * pose.Size;
                if (!FitsSize(region, pose.Layouts[anchor], scale)) continue;
                double bodyX = region.Center.X - pose.Layouts[anchor].X * scale;
                double bodyY = region.Center.Y - pose.Layouts[anchor].Y * scale;
                double tolerance = Math.Max(6, Math.Max(_profile.Body.Bounds.Width, _profile.Body.Bounds.Height) * scale * .6);
                matches.Clear();
                matches[anchor] = new(region, 0, region.Count / (_signatures[anchor].Pixels * scale * scale * pose.Aspect));
                int count = 1;
                for (int target = 0; target < groups.Length; target++)
                {
                    if (target == anchor) continue;
                    // A detected body defines its own scale. Other anchors handle hypotheses without
                    // body color, rather than fitting a tiny brown fragment to a much larger feather.
                    if (target == 0) continue;
                    matches[target] = Locate(bounds, region, groups[target], _signatures[target], pose.Layouts[target],
                        bodyX, bodyY, scale, pose.Aspect, tolerance);
                    if (matches[target] != null) count++;
                }
                if (count < 2) continue;
                if (count == 2 && (matches[0] is { Region.Density: < .5 } || !StrongPair(matches))) continue;
                double score = Score(matches, count);
                if (score < minimumPercent / 100.0 || best != null &&
                    score + .03 * count + (matches[0] != null ? .01 : 0) <= Evidence(best)) continue;
                var inferred = matches[0] == null ? FitAnchor(matches, pose.Aspect) : null;
                if (matches[0] == null && inferred == null) continue;
                if (inferred is { } fit && !FitsMass(matches, fit.Scale, pose.Aspect, count == 2 ? .2 : .12)) continue;
                var point = matches[0]?.Region.Center ?? inferred!.Value.Point;
                if (!bounds.Contains(point)) continue;
                var matchBounds = region.Bounds;
                var features = BobberFeatures.None;
                for (int i = 0; i < matches.Length; i++)
                    if (matches[i] is { } match) { matchBounds = matchBounds.Union(match.Region.Bounds); features |= Features[i]; }
                // Include the inferred body point even when its brown color is missing under the current light.
                matchBounds = matchBounds.Union(new(point.X, point.Y, 1, 1));
                best = new(matchBounds, point, score, inferred?.Scale ?? scale, features);
            }
        }
        return best;
    }

    private static bool StrongPair(ReadOnlySpan<FeatureMatch?> matches)
    {
        foreach (var match in matches) if (match is { IsStrong: false }) return false;
        return true;
    }

    private static bool FitsSize(BobberRegion region, Layout layout, double scale)
        => region.Bounds.Width >= layout.Width * scale * .45 && region.Bounds.Height >= layout.Height * scale * .45 &&
            region.Bounds.Width <= layout.Width * scale * 1.65 && region.Bounds.Height <= layout.Height * scale * 1.65;

    private bool FitsMass(ReadOnlySpan<FeatureMatch?> matches, double scale, double aspect, double minimum)
    {
        for (int i = 0; i < matches.Length; i++)
            if (matches[i] is { } match)
            {
                double ratio = match.Region.Count / (_signatures[i].Pixels * scale * scale * aspect);
                if (ratio < minimum || ratio > 3.2) return false;
            }
        return true;
    }

    private (PixelPoint Point, double Scale)? FitAnchor(ReadOnlySpan<FeatureMatch?> matches, double aspect)
    {
        // Refine against all observed centers: extrapolating from one coarse scan pose would move the
        // inferred body point noticeably when only the two small features above it are visible.
        double sx = 0, sy = 0, tx = 0, ty = 0;
        int count = 0;
        for (int i = 0; i < matches.Length; i++)
            if (matches[i] is { } match)
            {
                sx += _signatures[i].Center.X * aspect; sy += _signatures[i].Center.Y;
                tx += match.Region.Center.X; ty += match.Region.Center.Y; count++;
            }
        sx /= count; sy /= count; tx /= count; ty /= count;
        double dot = 0, cross = 0, spread = 0;
        for (int i = 0; i < matches.Length; i++)
            if (matches[i] is { } match)
            {
                double x = _signatures[i].Center.X * aspect - sx, y = _signatures[i].Center.Y - sy;
                double u = match.Region.Center.X - tx, v = match.Region.Center.Y - ty;
                dot += x * u + y * v; cross += x * v - y * u; spread += x * x + y * y;
            }
        if (spread < 1) return null;
        double a = dot / spread, b = cross / spread;
        double scale = Math.Sqrt(a * a + b * b);
        if (scale is < .28 or > 2.76 || Math.Abs(Math.Atan2(b, a)) > 35 * Math.PI / 180) return null;
        double dx = _profile.Anchor.X * aspect - sx, dy = _profile.Anchor.Y - sy;
        return (new((int)Math.Round(tx + a * dx - b * dy), (int)Math.Round(ty + b * dx + a * dy)), scale);
    }

    private static FeatureMatch? Locate(PixelRect frame, BobberRegion anchor, List<BobberRegion> groups,
        BobberColorSignature signature, Layout layout, double bodyX, double bodyY, double scale, double aspect, double tolerance)
    {
        var expected = new PixelPoint((int)Math.Round(bodyX + layout.X * scale), (int)Math.Round(bodyY + layout.Y * scale));
        double padding = tolerance * .3 + 2;
        int rx = (int)Math.Ceiling(layout.Width * scale / 2 + padding), ry = (int)Math.Ceiling(layout.Height * scale / 2 + padding);
        var area = new PixelRect(expected.X - rx, expected.Y - ry, rx * 2 + 1, ry * 2 + 1).Intersect(frame);
        double expectedCount = signature.Pixels * scale * scale * aspect;
        BobberRegion? selected = null;
        double distance = double.MaxValue;
        foreach (var region in groups)
        {
            // Never count the same surface twice, or crop a small piece out of a large water reflection.
            if (region.Count < Math.Max(4, expectedCount * .12) || region.Count > expectedCount * 3.2 ||
                !FitsSize(region, layout, scale) || !region.IsSeparateFrom(anchor) || !area.Contains(region.Bounds)) continue;
            double error = region.Center.DistanceSquaredTo(expected);
            if (error >= distance) continue;
            selected = region; distance = error;
        }
        double normalized = Math.Sqrt(distance) / tolerance;
        return selected is { } found && normalized <= 1.5 ? new(found, normalized * normalized, found.Count / expectedCount) : null;
    }

    private static double Score(ReadOnlySpan<FeatureMatch?> matches, int count)
    {
        double colors = 0, errors = 0, masses = 0;
        foreach (var match in matches)
            if (match is { } m) { colors += m.Region.Quality; errors += m.ErrorSquared; masses += m.MassError; }
        double geometry = Math.Exp(-.44 * errors / (count - 1)), mass = Math.Exp(-.16 * masses / count);
        return .7 * colors / count + .22 * geometry + .08 * mass - (count == 2 ? .04 : 0);
    }

    private SearchPose[] CreatePoses()
    {
        var poses = new List<SearchPose>(45);
        foreach (double angle in new[] { -30.0, -15, 0, 15, 30 })
        {
            double radians = angle * Math.PI / 180, cos = Math.Cos(radians), sin = Math.Sin(radians);
            foreach (double size in new[] { .8, 1, 1.2 })
            foreach (double aspect in new[] { .8, 1, 1.2 })
            {
                Layout LayoutFor(BobberColorSignature signature)
                {
                    double dx = (signature.Center.X - _profile.Body.Center.X) * aspect;
                    double dy = signature.Center.Y - _profile.Body.Center.Y;
                    return new(dx * cos - dy * sin, dx * sin + dy * cos,
                        signature.Bounds.Width * Math.Abs(cos) * aspect + signature.Bounds.Height * Math.Abs(sin),
                        signature.Bounds.Width * Math.Abs(sin) * aspect + signature.Bounds.Height * Math.Abs(cos));
                }
                poses.Add(new(size, aspect, _signatures.Select(LayoutFor).ToArray()));
            }
        }
        return poses.ToArray();
    }
}
