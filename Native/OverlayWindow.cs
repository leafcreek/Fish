using System.ComponentModel;
using System.Runtime.InteropServices;
using Fish.Core;

namespace Fish.Native;

// Owned and updated on the WinUI dispatcher thread. The region contains only the border.
internal sealed class OverlayWindow : IDisposable
{
    private delegate nint WindowProc(nint window, uint message, nint wParam, nint lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Style; public WindowProc Procedure; public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background; public string? MenuName; public string ClassName;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassW(ref WindowClass cls);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClassW(string name, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowExW(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] private static extern nint DefWindowProcW(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(nint window, uint color, byte alpha, uint flags);
    [DllImport("gdi32.dll")] private static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern int CombineRgn(nint dest, nint source1, nint source2, int mode);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint window, nint region, bool redraw);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandleW(string? module);
    private readonly WindowProc _procedure = DefWindowProcW;
    private readonly string _className = "FishOverlay_" + Guid.NewGuid().ToString("N");
    private readonly nint _instance = GetModuleHandleW(null);
    private nint _window, _brush;
    private PixelRect? _bounds;

    public OverlayWindow(uint color)
    {
        _brush = CreateSolidBrush(color);
        var cls = new WindowClass { Procedure = _procedure, Instance = _instance, Background = _brush, ClassName = _className };
        if (RegisterClassW(ref cls) == 0) { Desktop.DeleteObject(_brush); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        _window = CreateWindowExW(0x080800A0, _className, "", 0x80000000, 0, 0, 1, 1, 0, 0, _instance, 0);
        if (_window == 0) { Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        SetLayeredWindowAttributes(_window, 0, 255, 2);
    }
    public void Show(PixelRect? bounds)
    {
        if (bounds is not { Width: > 0, Height: > 0 } rect) { Hide(); return; }
        if (_bounds != rect)
        {
            int w = rect.Width + 4, h = rect.Height + 4;
            nint outer = CreateRectRgn(0, 0, w, h), inner = CreateRectRgn(2, 2, w - 2, h - 2);
            try
            {
                if (outer == 0 || inner == 0 || CombineRgn(outer, outer, inner, 4) == 0 || SetWindowRgn(_window, outer, true) == 0)
                    throw new Win32Exception("Overlay-Bereich konnte nicht erstellt werden.");
                outer = 0; // Successful SetWindowRgn transfers ownership to Windows.
            }
            finally { if (outer != 0) Desktop.DeleteObject(outer); if (inner != 0) Desktop.DeleteObject(inner); }
            _bounds = rect;
        }
        Desktop.SetWindowPos(_window, -1, rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4, 0x0010 | 0x0040);
    }
    public void Hide() { if (_window != 0) Desktop.ShowWindow(_window, 0); }
    public void Dispose()
    {
        if (_window != 0) { DestroyWindow(_window); _window = 0; }
        UnregisterClassW(_className, _instance);
        if (_brush != 0) { Desktop.DeleteObject(_brush); _brush = 0; }
    }
}
