using Fish.Configuration;

namespace Fish.Core;

public enum RegionEditKind { Move, Resize }

/// <summary>Screen-space drag geometry, constrained to the game client and the supported search sizes.</summary>
public readonly record struct RegionEdit(PixelRect Initial, PixelRect Client, PixelPoint Pointer, RegionEditKind Kind)
{
    private const int Minimum = 100, MaximumWidth = 3840, MaximumHeight = 2160;
    public PixelRect At(PixelPoint pointer)
    {
        if (Client.Width < Minimum || Client.Height < Minimum) throw new InvalidOperationException("Die Spielfläche ist zu klein zum Bearbeiten.");
        int width = Math.Clamp(Initial.Width, Minimum, Math.Min(MaximumWidth, Client.Width));
        int height = Math.Clamp(Initial.Height, Minimum, Math.Min(MaximumHeight, Client.Height));
        int x = Math.Clamp(Initial.X, Client.X, Client.X + Client.Width - width);
        int y = Math.Clamp(Initial.Y, Client.Y, Client.Y + Client.Height - height);
        int dx = pointer.X - Pointer.X, dy = pointer.Y - Pointer.Y;
        return Kind == RegionEditKind.Move
            ? new(Math.Clamp(x + dx, Client.X, Client.X + Client.Width - width),
                Math.Clamp(y + dy, Client.Y, Client.Y + Client.Height - height), width, height)
            : new(x, y, Math.Clamp(width + dx, Minimum, Math.Min(MaximumWidth, Client.X + Client.Width - x)),
                Math.Clamp(height + dy, Minimum, Math.Min(MaximumHeight, Client.Y + Client.Height - y)));
    }

    public static SearchRegion ToRegion(PixelRect area, PixelRect client) => new(area.Width, area.Height,
        area.X + area.Width / 2 - client.X - client.Width / 2,
        client.Y + client.Height / 2 - area.Y - area.Height / 2);
}
