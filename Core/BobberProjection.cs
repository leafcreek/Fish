namespace Fish.Core;

internal readonly record struct BobberPose(double Scale, double RotationDegrees, double AspectRatio, double Perspective);

/// <summary>A planar projective approximation; it cannot synthesize unseen sides of a 3D bobber.</summary>
internal sealed class BobberProjection
{
    private readonly double _cx, _cy, _cos, _sin, _minX, _minY;
    public BobberPose Pose { get; }
    public int Width { get; }
    public int Height { get; }
    public PixelPoint Anchor { get; }

    public BobberProjection(BobberTemplate template, BobberPose pose)
    {
        Pose = pose;
        _cx = (template.Image.Width - 1) / 2.0;
        _cy = (template.Image.Height - 1) / 2.0;
        double radians = pose.RotationDegrees * Math.PI / 180;
        _cos = Math.Cos(radians); _sin = Math.Sin(radians);
        var corners = new[] { Project(0, 0), Project(template.Image.Width - 1, 0),
            Project(0, template.Image.Height - 1), Project(template.Image.Width - 1, template.Image.Height - 1) };
        _minX = corners.Min(p => p.X); _minY = corners.Min(p => p.Y);
        Width = (int)Math.Ceiling(corners.Max(p => p.X) - _minX - 1e-9) + 1;
        Height = (int)Math.Ceiling(corners.Max(p => p.Y) - _minY - 1e-9) + 1;
        var anchor = Project(template.Anchor.X, template.Anchor.Y);
        Anchor = new((int)Math.Round(anchor.X - _minX), (int)Math.Round(anchor.Y - _minY));
    }

    public (double X, double Y) Source(double x, double y)
    {
        x = (x + _minX) / Pose.Scale; y = (y + _minY) / Pose.Scale;
        double u = _cos * x + _sin * y, v = -_sin * x + _cos * y;
        double denominator = 1 - Pose.Perspective * v / _cy;
        if (denominator <= 0) return (double.NaN, double.NaN);
        double sourceY = v / denominator;
        double depth = 1 + Pose.Perspective * sourceY / _cy;
        return (u * depth / Pose.AspectRatio + _cx, sourceY + _cy);
    }

    public (double X, double Y) Output(double x, double y)
    {
        var point = Project(x, y);
        return (point.X - _minX, point.Y - _minY);
    }

    private (double X, double Y) Project(double x, double y)
    {
        x -= _cx; y -= _cy;
        double depth = 1 + Pose.Perspective * y / _cy;
        double u = Pose.Scale * Pose.AspectRatio * x / depth, v = Pose.Scale * y / depth;
        return (_cos * u - _sin * v, _sin * u + _cos * v);
    }
}
