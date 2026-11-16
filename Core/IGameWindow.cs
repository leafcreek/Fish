namespace Fish.Core;

public interface IGameWindow
{
    nint FindGame();
    bool IsAvailable(nint game);
    PixelRect? GetGameBounds(nint game);
}
