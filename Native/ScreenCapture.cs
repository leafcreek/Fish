using System.ComponentModel;
using System.Runtime.InteropServices;
using Fish.Core;

namespace Fish.Native;

/// <summary>Serial capture with a reusable DIB. Every returned frame owns independent managed pixels.</summary>
internal sealed class ScreenCapture : IDisposable
{
    private nint _dc, _bitmap, _original, _bits;
    private int _width, _height;
    private bool _disposed;

    public PixelFrame Capture(PixelRect bounds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (bounds.IsEmpty) throw new ArgumentException("Der Suchbereich ist leer.");
        nint screen = Desktop.GetDC(0);
        if (screen == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            if (_width != bounds.Width || _height != bounds.Height || _dc == 0) Allocate(screen, bounds.Width, bounds.Height);
            if (!Desktop.BitBlt(_dc, 0, 0, bounds.Width, bounds.Height, screen, bounds.X, bounds.Y, 0x00CC0020))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            Desktop.GdiFlush();
            var pixels = new int[checked(bounds.Width * bounds.Height)];
            Marshal.Copy(_bits, pixels, 0, pixels.Length);
            return new(bounds.Width, bounds.Height, pixels);
        }
        finally { Desktop.ReleaseDC(0, screen); }
    }
    private void Allocate(nint screen, int width, int height)
    {
        Release();
        try
        {
            _dc = Desktop.CreateCompatibleDC(screen);
            if (_dc == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            var info = new Desktop.BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            _bitmap = Desktop.CreateDIBSection(screen, ref info, 0, out _bits, 0, 0);
            if (_bitmap == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            _original = Desktop.SelectObject(_dc, _bitmap);
            if (_original == 0 || _original == -1) throw new Win32Exception("Bildpuffer konnte nicht ausgewählt werden.");
            _width = width; _height = height;
        }
        catch { Release(); throw; }
    }
    private void Release()
    {
        if (_original != 0 && _original != -1) Desktop.SelectObject(_dc, _original);
        if (_bitmap != 0) Desktop.DeleteObject(_bitmap);
        if (_dc != 0) Desktop.DeleteDC(_dc);
        _dc = _bitmap = _original = _bits = 0; _width = _height = 0;
    }
    public void Dispose() { if (_disposed) return; Release(); _disposed = true; }
}
