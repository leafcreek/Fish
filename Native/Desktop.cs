using System.ComponentModel;
using System.Runtime.InteropServices;
using Fish.Core;

namespace Fish.Native;

internal static class Desktop
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorData
    {
        public int Size; public Rect Monitor, Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    private delegate bool MonitorCallback(nint monitor, nint dc, ref Rect rect, nint data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfoW(nint monitor, ref MonitorData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint FindWindowW(string? className, string title);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint window);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool PostMessageW(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] internal static extern bool ScreenToClient(nint window, ref Point point);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(nint window, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool ClientToScreen(nint window, ref Point point);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint GetDC(nint window);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] internal static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern bool BitBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, uint operation);
    [DllImport("gdi32.dll")] internal static extern bool GdiFlush();
    [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, SizeImage; public int XPelsPerMeter, YPelsPerMeter;
        public uint ClrUsed, ClrImportant, Color;
    }
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint window);
    public static MonitorInfo[] Monitors()
    {
        var monitors = new List<MonitorInfo>();
        MonitorCallback callback = (nint handle, nint dc, ref Rect rect, nint data) =>
        {
            var info = new MonitorData { Size = Marshal.SizeOf<MonitorData>(), Device = "" };
            if (GetMonitorInfoW(handle, ref info)) monitors.Add(new(info.Device,
                new(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top),
                new(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top), (info.Flags & 1) != 0));
            return true;
        };
        if (!EnumDisplayMonitors(0, 0, callback, 0)) throw new Win32Exception("Monitore konnten nicht ermittelt werden.");
        return monitors.ToArray();
    }
}
