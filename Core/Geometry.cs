namespace Fish.Core;

public readonly record struct PixelPoint(int X, int Y)
{
    public double DistanceSquaredTo(PixelPoint other)
    {
        double dx = (double)X - other.X, dy = (double)Y - other.Y;
        return dx * dx + dy * dy;
    }
}
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => Width <= 0 || Height <= 0;
    [System.Text.Json.Serialization.JsonIgnore]
    public int Area => IsEmpty ? 0 : checked(Width * Height);
    public bool Contains(PixelPoint point) => point.X >= X && point.Y >= Y && point.X < X + Width && point.Y < Y + Height;
    public bool Contains(PixelRect other) => !other.IsEmpty && other.X >= X && other.Y >= Y &&
        other.X + other.Width <= X + Width && other.Y + other.Height <= Y + Height;
    public PixelRect Union(PixelRect other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;
        int x = Math.Min(X, other.X), y = Math.Min(Y, other.Y);
        return new(x, y, Math.Max(X + Width, other.X + other.Width) - x, Math.Max(Y + Height, other.Y + other.Height) - y);
    }
    public PixelRect Intersect(PixelRect other)
    {
        int x = Math.Max(X, other.X), y = Math.Max(Y, other.Y);
        return new(x, y, Math.Max(0, Math.Min(X + Width, other.X + other.Width) - x),
            Math.Max(0, Math.Min(Y + Height, other.Y + other.Height) - y));
    }
    public static PixelRect Around(PixelPoint center, int width, int height, int offsetX, int offsetY, PixelRect bounds)
        => new PixelRect(center.X - width / 2 + offsetX, center.Y - height / 2 - offsetY, width, height).Intersect(bounds);
}
public sealed record MonitorInfo(string Device, PixelRect Bounds, PixelRect? WorkArea = null, bool IsPrimary = false);
public sealed record PixelFrame(int Width, int Height, int[] Pixels);
