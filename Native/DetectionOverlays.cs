using Fish.Configuration;
using Fish.Core;

namespace Fish.Native;

/// <summary>Interactive bobber frame and a display-only, click-through splash frame.</summary>
internal sealed class DetectionOverlays(IGameWindow? windows = null) : IDisposable
{
    // COLORREF values (BGR); paired with the Schwimmer/Biss accents in the app.
    private const uint BobberColor = 0xA8B621, SplashColor = 0x4DB8F3, HandleColor = 0x737F08;
    private readonly IGameWindow _windows = windows ?? new GameWindow.Access();
    private OverlayWindow? _bobber, _splash, _move, _resize;
    private nint _game;
    private PixelRect _client, _area;
    private SearchRegion _configured, _originalRegion;
    private RegionEdit? _edit;
    public bool IsEditing => _edit != null;
    public event Action<SearchRegion, bool>? BobberRegionChanged;
    public event Action<bool>? EditingChanged;
    public event Action<Exception>? InteractionFailed;

    public void Update(BotOptions options, BotUpdate? displayed, bool running)
    {
        nint game = _windows.FindGame();
        if (!_windows.IsAvailable(game) || _windows.GetGameBounds(game) is not { } client) { Hide(); return; }
        if (IsEditing && (_game != game || _client != client)) CancelEdit();
        _game = game; _client = client;
        if (!IsEditing) _configured = new(options.BobberAreaWidth, options.BobberAreaHeight, options.BobberAreaXOffset, options.BobberAreaYOffset);
        _area = _configured.In(client);
        _bobber ??= new(BobberColor);
        DrawBobber();
        bool showSplash = !IsEditing && running && displayed?.Game == game && displayed.BobberArea == _area &&
            displayed.SplashArea != null && displayed.Bobber != null;
        var splash = showSplash ? FishingDefaults.SplashRegion.Around(displayed!.Bobber!.Value, client) : (PixelRect?)null;
        if (!_area.IsEmpty && splash != null) { _splash ??= new(SplashColor); _splash.Show(splash); }
        else _splash?.Hide();
    }

    private OverlayWindow CreateHandle(RegionEditKind kind)
    {
        var handle = new OverlayWindow(HandleColor, kind);
        handle.Dragged += drag => OnDrag(handle, drag);
        handle.InteractionFailed += error => InteractionFailed?.Invoke(error);
        return handle;
    }

    private void DrawBobber()
    {
        _bobber?.Show(_area);
        if (_bobber == null || _area.IsEmpty || _area.Width < 100 || _area.Height < 100)
        { _move?.Hide(); _resize?.Hide(); return; }
        int size = Math.Clamp((int)Math.Round(22 * Desktop.GetDpiForWindow(_bobber.Handle) / 96.0), 20, 64);
        _move ??= CreateHandle(RegionEditKind.Move);
        _resize ??= CreateHandle(RegionEditKind.Resize);
        _move.Show(new(_area.X + 2, _area.Y + 2, size, size));
        _resize.Show(new(_area.X + _area.Width - size - 2, _area.Y + _area.Height - size - 2, size, size));
    }

    private void OnDrag(OverlayWindow handle, OverlayDrag drag)
    {
        if (drag.Phase == OverlayDragPhase.Began)
        {
            if (IsEditing || _area.IsEmpty || _windows.FindGame() != _game || !_windows.IsAvailable(_game) || _windows.GetGameBounds(_game) != _client)
            { handle.CancelDrag(); return; }
            _originalRegion = _configured;
            _edit = new(_area, _client, drag.Pointer, drag.Kind);
            _splash?.Hide(); EditingChanged?.Invoke(true);
            return;
        }
        if (_edit is not { } edit) return;
        bool cancelled = drag.Phase == OverlayDragPhase.Cancelled || _windows.FindGame() != _game || !_windows.IsAvailable(_game) || _windows.GetGameBounds(_game) != _client;
        bool finished = cancelled || drag.Phase == OverlayDragPhase.Completed;
        _configured = cancelled ? _originalRegion : RegionEdit.ToRegion(edit.At(drag.Pointer), _client);
        _area = _configured.In(_client);
        if (finished) _edit = null;
        try { BobberRegionChanged?.Invoke(_configured, finished); DrawBobber(); }
        finally { if (finished) EditingChanged?.Invoke(false); }
        if (cancelled) handle.CancelDrag();
    }

    private void CancelEdit() { _move?.CancelDrag(); _resize?.CancelDrag(); }
    public void Hide() { CancelEdit(); _bobber?.Hide(); _splash?.Hide(); _move?.Hide(); _resize?.Hide(); }
    public void Dispose() { CancelEdit(); _move?.Dispose(); _resize?.Dispose(); _bobber?.Dispose(); _splash?.Dispose(); }
}
