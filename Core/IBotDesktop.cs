namespace Fish.Core;

public interface IBotDesktop
{
    nint FindGame();
    bool IsAvailable(nint game);
    PixelRect? GetGameBounds(nint game);
    PixelFrame Capture(PixelRect bounds);
    void Key(nint game, int key, CancellationToken cancellation);
    Task LootAsync(nint game, PixelPoint point, PixelRect expectedBounds, CancellationToken cancellation);
    void CloseGame(nint game, CancellationToken cancellation);
    void Shutdown(CancellationToken cancellation);
}
