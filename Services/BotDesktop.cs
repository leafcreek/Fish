using System.ComponentModel;
using System.Runtime.InteropServices;
using Fish.Core;
using Fish.Native;

namespace Fish.Services;

public sealed class BotDesktop : IBotDesktop, IDisposable
{
    private readonly ScreenCapture _capture = new();
    public nint FindGame() => GameWindow.Find();
    public bool IsAvailable(nint game) => GameWindow.IsAvailable(game);
    public PixelRect? GetGameBounds(nint game) => GameWindow.Bounds(game);
    public PixelFrame Capture(PixelRect bounds) => _capture.Capture(bounds);
    private bool CanInput(nint game, PixelRect expected) => FindGame() == game && IsAvailable(game) && GetGameBounds(game) == expected;
    private static void RestoreFocus(nint game, nint previous)
    {
        if (previous != 0 && previous != game && Desktop.GetForegroundWindow() == game && Desktop.IsWindow(previous))
            Desktop.SetForegroundWindow(previous);
    }
    public async Task<bool> CastAsync(nint game, FishingHotkey hotkey, PixelRect expectedBounds, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!CanInput(game, expectedBounds) || KeyboardInput.UserIsTyping(hotkey)) return false;
        var previous = Desktop.GetForegroundWindow();
        if (!Desktop.SetForegroundWindow(game)) return false;
        try
        {
            if (Desktop.GetForegroundWindow() != game || !CanInput(game, expectedBounds)) return false;
            KeyboardInput.Send(hotkey, cancellation);
            // Let Windows dispatch the atomic key batch before returning focus to Fish.
            await Task.Delay(25).ConfigureAwait(false);
            return true;
        }
        finally { RestoreFocus(game, previous); }
    }
    public async Task<bool> LootAsync(nint game, PixelPoint point, PixelRect expectedBounds, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!CanInput(game, expectedBounds) || !expectedBounds.Contains(point)) return false;
        var previous = Desktop.GetForegroundWindow();
        if (!Desktop.SetForegroundWindow(game)) return false;
        try
        {
            await Task.Delay(25, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (Desktop.GetForegroundWindow() != game || !CanInput(game, expectedBounds)) return false;
            var client = new Desktop.Point { X = point.X, Y = point.Y };
            if (!Desktop.ScreenToClient(game, ref client)) return false;
            nint position = (client.Y << 16) | (client.X & 0xffff);
            Desktop.PostMessageW(game, 0x200, 0, position);
            if (!Desktop.PostMessageW(game, 0x204, 2, position)) throw new Win32Exception(Marshal.GetLastWin32Error());
            // Always release the right button, including a concurrent Stop.
            if (!Desktop.PostMessageW(game, 0x205, 0, position)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return true;
        }
        finally { RestoreFocus(game, previous); }
    }
    public void Dispose() => _capture.Dispose();
}
