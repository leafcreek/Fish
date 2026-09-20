using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Fish.Core;
using Fish.Native;

namespace Fish.Services;

public sealed class BotDesktop : IBotDesktop
{
    public nint FindGame() => Desktop.FindWindowW(null, "World of Warcraft");
    public bool IsAvailable(nint game) => Desktop.IsWindow(game) && Desktop.IsWindowVisible(game) && !Desktop.IsIconic(game);
    public PixelRect? GetGameBounds(nint game)
    {
        if (game == 0 || !Desktop.IsWindow(game) || Desktop.IsIconic(game) ||
            !Desktop.GetClientRect(game, out var rect)) return null;
        var origin = new Desktop.Point { X = rect.Left, Y = rect.Top };
        if (!Desktop.ClientToScreen(game, ref origin) || rect.Right <= rect.Left || rect.Bottom <= rect.Top) return null;
        return new(origin.X, origin.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
    public PixelFrame Capture(PixelRect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) throw new ArgumentException("Der Suchbereich liegt außerhalb des Monitors.");
        nint screen = Desktop.GetDC(0), dc = 0, bitmap = 0, previous = 0;
        try
        {
            if (screen == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            dc = Desktop.CreateCompatibleDC(screen);
            if (dc == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            var info = new Desktop.BitmapInfo { Size = 40, Width = bounds.Width, Height = -bounds.Height, Planes = 1, BitCount = 32 };
            bitmap = Desktop.CreateDIBSection(screen, ref info, 0, out var bits, 0, 0);
            if (bitmap == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            previous = Desktop.SelectObject(dc, bitmap);
            if (previous == 0 || previous == -1) throw new Win32Exception("Bildpuffer konnte nicht ausgewählt werden.");
            if (!Desktop.BitBlt(dc, 0, 0, bounds.Width, bounds.Height, screen, bounds.X, bounds.Y, 0x00CC0020))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            Desktop.GdiFlush();
            int[] pixels = new int[checked(bounds.Width * bounds.Height)];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            return new(bounds.Width, bounds.Height, pixels);
        }
        finally
        {
            if (previous != 0 && previous != -1) Desktop.SelectObject(dc, previous);
            if (bitmap != 0) Desktop.DeleteObject(bitmap);
            if (dc != 0) Desktop.DeleteDC(dc);
            if (screen != 0) Desktop.ReleaseDC(0, screen);
        }
    }
    public void Key(nint game, int key, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (FindGame() != game) return;
        if (!Desktop.PostMessageW(game, 0x100, key, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        // Release a key even if cancellation arrives after its down message.
        Desktop.PostMessageW(game, 0x101, key, 0);
    }
    public async Task LootAsync(nint game, PixelPoint point, PixelRect expectedBounds, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (GetGameBounds(game) != expectedBounds) return;
        var previous = Desktop.GetForegroundWindow();
        if (!Desktop.SetForegroundWindow(game)) return;
        try
        {
            await Task.Delay(25, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (FindGame() != game || Desktop.GetForegroundWindow() != game || GetGameBounds(game) != expectedBounds) return;
            if (point.X < expectedBounds.X || point.X >= expectedBounds.X + expectedBounds.Width ||
                point.Y < expectedBounds.Y || point.Y >= expectedBounds.Y + expectedBounds.Height) return;
            var client = new Desktop.Point { X = point.X, Y = point.Y };
            if (!Desktop.ScreenToClient(game, ref client)) return;
            nint position = (client.Y << 16) | (client.X & 0xffff);
            Desktop.PostMessageW(game, 0x200, 0, position);
            if (!Desktop.PostMessageW(game, 0x204, 2, position)) throw new Win32Exception(Marshal.GetLastWin32Error());
            Desktop.PostMessageW(game, 0x205, 0, position);
        }
        finally
        {
            if (previous != 0 && previous != game && Desktop.GetForegroundWindow() == game && Desktop.IsWindow(previous))
                Desktop.SetForegroundWindow(previous);
        }
    }
    public void CloseGame(nint game, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (game != 0 && FindGame() == game && !Desktop.PostMessageW(game, 0x10, 0, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Shutdown(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0") { UseShellExecute = false, CreateNoWindow = true });
    }
}
