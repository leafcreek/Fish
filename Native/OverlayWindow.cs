using System.ComponentModel;
using System.Runtime.InteropServices;
using Fish.Core;

namespace Fish.Native;

internal enum OverlayDragPhase { Began, Moved, Completed, Cancelled }
internal readonly record struct OverlayDrag(OverlayDragPhase Phase, RegionEditKind Kind, PixelPoint Pointer);

// Dispatcher-owned: click-through frame, or a small interactive corner handle.
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
    [DllImport("gdi32.dll")] private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);
    [DllImport("gdi32.dll")] private static extern int CombineRgn(nint dest, nint source1, nint source2, int mode);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint window, nint region, bool redraw);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandleW(string? module);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint LoadCursorW(nint instance, nint name);
    [DllImport("user32.dll")] private static extern uint GetMessagePos();
    [DllImport("user32.dll")] private static extern nint SetCapture(nint window);
    [DllImport("user32.dll")] private static extern nint GetCapture();
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(nint window, uint affinity);
    [StructLayout(LayoutKind.Sequential)] private struct PaintData
    {
        public nint Dc; public int Erase; public Desktop.Rect Rect; public int Restore, IncUpdate;
        public uint R0, R1, R2, R3, R4, R5, R6, R7;
    }
    [DllImport("user32.dll")] private static extern nint BeginPaint(nint window, out PaintData data);
    [DllImport("user32.dll")] private static extern bool EndPaint(nint window, ref PaintData data);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeout, uint charSet, uint outputPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(nint dc, uint color);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(nint dc, int mode);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Desktop.Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DrawTextW(nint dc, string text, int length, ref Desktop.Rect rect, uint format);
    private readonly WindowProc _procedure;
    private readonly string _className;
    private readonly RegionEditKind? _handle;
    private readonly nint _instance = GetModuleHandleW(null);
    private nint _window, _brush, _font, _previousForeground;
    private int _fontHeight;
    private PixelRect? _bounds;
    private bool _dragging;
    private PixelPoint _lastPointer;
    public nint Handle => _window;
    public event Action<OverlayDrag>? Dragged;
    public event Action<Exception>? InteractionFailed;

    public OverlayWindow(uint color, RegionEditKind? handle = null)
    {
        _handle = handle;
        _procedure = handle != null ? HandleMessage : DefWindowProcW;
        _className = (handle != null ? "FishOverlayHandle_" : "FishOverlay_") + Guid.NewGuid().ToString("N");
        _brush = CreateSolidBrush(color);
        var cls = new WindowClass { Procedure = _procedure, Instance = _instance, Background = _brush, ClassName = _className,
            Cursor = handle != null ? LoadCursorW(0, handle == RegionEditKind.Move ? 32646 : 32642) : 0 };
        if (RegisterClassW(ref cls) == 0)
        {
            Desktop.DeleteObject(_brush);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        _window = CreateWindowExW(handle != null ? 0x00080080u : 0x080800A0u, _className,
            handle == RegionEditKind.Move ? "Schwimmer-Bereich verschieben" : handle == RegionEditKind.Resize ? "Schwimmer-Bereich skalieren" : "",
            0x80000000, 0, 0, 1, 1, 0, 0, _instance, 0);
        if (_window == 0) { Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        SetLayeredWindowAttributes(_window, 0, handle != null ? (byte)255 : (byte)230, 2);
        if (handle != null) SetWindowDisplayAffinity(_window, 0x11); // Keep control pixels out of captures on supported Windows versions.
    }
    public void Show(PixelRect? bounds)
    {
        if (bounds is not { Width: > 0, Height: > 0 } rect) { Hide(); return; }
        if (_bounds == null || _bounds.Value.Width != rect.Width || _bounds.Value.Height != rect.Height)
        {
            int w = rect.Width + (_handle == null ? 4 : 0), h = rect.Height + (_handle == null ? 4 : 0);
            int radius = Math.Max(4, (int)Math.Round(8 * Desktop.GetDpiForWindow(_window) / 96.0));
            nint outer = _handle == null ? CreateRectRgn(0, 0, w, h) : CreateRoundRectRgn(0, 0, w + 1, h + 1, radius, radius);
            // Keep every pixel inside the search rectangle clear, including its corners.
            nint inner = _handle == null ? CreateRectRgn(2, 2, w - 2, h - 2) : 0;
            try
            {
                if (outer == 0 || _handle == null && (inner == 0 || CombineRgn(outer, outer, inner, 4) == 0) || SetWindowRgn(_window, outer, true) == 0)
                    throw new Win32Exception("Overlay-Bereich konnte nicht erstellt werden.");
                outer = 0; // Successful SetWindowRgn transfers ownership to Windows.
            }
            finally { if (outer != 0) Desktop.DeleteObject(outer); if (inner != 0) Desktop.DeleteObject(inner); }
        }
        _bounds = rect;
        int padding = _handle == null ? 2 : 0;
        Desktop.SetWindowPos(_window, -1, rect.X - padding, rect.Y - padding, rect.Width + padding * 2, rect.Height + padding * 2, 0x0010 | 0x0040);
    }
    private PixelPoint Pointer()
    {
        uint position = GetMessagePos();
        return _lastPointer = new((short)(position & 0xffff), (short)(position >> 16));
    }
    private nint HandleMessage(nint window, uint message, nint wParam, nint lParam)
    {
        try
        {
            switch (message)
            {
                case 0x21: _previousForeground = Desktop.GetForegroundWindow(); return 1; // WM_MOUSEACTIVATE / MA_ACTIVATE: reliable capture outside the handle.
                case 0x201: // WM_LBUTTONDOWN
                    if (_dragging) return 0;
                    _dragging = true;
                    Dragged?.Invoke(new(OverlayDragPhase.Began, _handle!.Value, Pointer()));
                    if (!_dragging) return 0;
                    if (Desktop.GetForegroundWindow() != window && !Desktop.SetForegroundWindow(window)) { CancelDrag(); return 0; }
                    SetCapture(window);
                    if (GetCapture() != window) CancelDrag();
                    return 0;
                case 0x200 when _dragging:
                    Dragged?.Invoke(new(OverlayDragPhase.Moved, _handle!.Value, Pointer())); return 0;
                case 0x202 when _dragging:
                    FinishDrag(OverlayDragPhase.Completed); return 0;
                case 0x100 when (int)wParam == 0x1b:
                case 0x1f: // WM_CANCELMODE
                case 0x215: // WM_CAPTURECHANGED
                case 0x10: // Do not destroy a control window through Alt+F4.
                    CancelDrag(); return 0;
                case 0xf:
                    PaintHandle(window); return 0;
            }
        }
        catch (Exception error)
        {
            // Managed exceptions must not unwind through a native window procedure.
            try { CancelDrag(); } catch { }
            try { InteractionFailed?.Invoke(error); } catch { }
            return 0;
        }
        return DefWindowProcW(window, message, wParam, lParam);
    }
    public void CancelDrag() { if (_dragging) FinishDrag(OverlayDragPhase.Cancelled); }
    private void FinishDrag(OverlayDragPhase phase)
    {
        var point = phase == OverlayDragPhase.Cancelled ? _lastPointer : Pointer();
        _dragging = false;
        try { Dragged?.Invoke(new(phase, _handle!.Value, point)); }
        finally
        {
            if (GetCapture() == _window) ReleaseCapture();
            if (_previousForeground != 0 && _previousForeground != _window && Desktop.GetForegroundWindow() == _window &&
                Desktop.IsWindow(_previousForeground) && Desktop.IsWindowVisible(_previousForeground) && !Desktop.IsIconic(_previousForeground))
                Desktop.SetForegroundWindow(_previousForeground);
            _previousForeground = 0;
        }
    }
    private void PaintHandle(nint window)
    {
        nint dc = BeginPaint(window, out var paint);
        nint old = 0;
        try
        {
            GetClientRect(window, out var rect);
            int height = Math.Max(1, Math.Min(rect.Right, rect.Bottom) - 1);
            if (_font == 0 || _fontHeight != height)
            {
                if (_font != 0) Desktop.DeleteObject(_font);
                _font = CreateFontW(-height, 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 4, 0, "Segoe UI Symbol");
                _fontHeight = height;
            }
            if (_font != 0) old = Desktop.SelectObject(dc, _font);
            SetTextColor(dc, 0xffffff);
            SetBkMode(dc, 1);
            DrawTextW(dc, _handle == RegionEditKind.Move ? "✥" : "◢", 1, ref rect, 0x25);
        }
        finally { if (old != 0) Desktop.SelectObject(dc, old); EndPaint(window, ref paint); }
    }
    public void Hide() { CancelDrag(); if (_window != 0) Desktop.ShowWindow(_window, 0); }
    public void Dispose()
    {
        CancelDrag();
        if (_window != 0) { DestroyWindow(_window); _window = 0; }
        UnregisterClassW(_className, _instance);
        if (_brush != 0) { Desktop.DeleteObject(_brush); _brush = 0; }
        if (_font != 0) { Desktop.DeleteObject(_font); _font = 0; }
    }
}
