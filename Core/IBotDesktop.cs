namespace Fish.Core;

public interface IBotDesktop : IGameWindow
{
    PixelFrame Capture(PixelRect bounds);
    Task<bool> CastAsync(nint game, FishingHotkey hotkey, PixelRect expectedBounds, CancellationToken cancellation);
    Task<bool> LootAsync(nint game, PixelPoint point, PixelRect expectedBounds, CancellationToken cancellation);
}
