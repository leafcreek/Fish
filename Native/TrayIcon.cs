using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Fish.Native;

internal sealed class TrayIcon : IDisposable
{
    private const uint CallbackMessage = 0x8001;
    private delegate nint SubclassProc(nint hwnd, uint msg, nint wParam, nint lParam, nuint id, nuint data);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct IconData
    {
        public uint Size; public nint Window; public uint Id, Flags, Callback; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIconW(uint message, ref IconData data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint window, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint window, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint window, uint message, nint wp, nint lp);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessageW(string message);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint LoadIconW(nint instance, nint resource);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern nint ExtractIconW(nint instance, string path, uint index);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenuW(nint menu, uint flags, nuint id, string text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint window, nint rect);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Desktop.Point point);
    private readonly nint _window, _icon;
    private readonly bool _ownsIcon;
    private readonly SubclassProc _proc;
    private readonly Action<int> _dispatch;
    private readonly uint _taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
    private bool _disposed;
    private IconData _data;
    public bool Available { get; private set; }
    public bool Running { get; set; }

    public TrayIcon(nint window, Action<int> dispatch)
    {
        _window = window; _dispatch = dispatch; _proc = Handle;
        _icon = ExtractIconW(0, Environment.ProcessPath!, 0);
        _ownsIcon = _icon != 0 && _icon != 1;
        if (!_ownsIcon) _icon = LoadIconW(0, 32512);
        _data = new() { Size = (uint)Marshal.SizeOf<IconData>(), Window = window, Id = 1, Flags = 7 | 128,
            Callback = CallbackMessage, Icon = _icon, Tip = "Fish · Bereit", Info = "", Title = "" };
        if (!SetWindowSubclass(window, _proc, 1, 0)) { if (_ownsIcon) DestroyIcon(_icon); throw new Win32Exception("Tray-Anbindung fehlgeschlagen."); }
        Add();
    }
    private void Add()
    {
        _data.Flags = 7 | 128;
        Available = Shell_NotifyIconW(0, ref _data);
        if (Available) { _data.Version = 4; Shell_NotifyIconW(4, ref _data); }
    }
    public void SetStatus(string text)
    {
        _data.Flags = 4 | 128; _data.Tip = ("Fish · " + text)[..Math.Min(127, text.Length + 7)];
        if (Available) Shell_NotifyIconW(1, ref _data);
    }
    private nint Handle(nint window, uint msg, nint wp, nint lp, nuint id, nuint data)
    {
        if (msg == _taskbarCreated) { Add(); if (!Available) _dispatch(1); return 0; }
        if (msg is 0x007e or 0x02e0) _dispatch(6); // Display layout / DPI changed.
        if (msg == 0x0005 && wp == 1) _dispatch(5); // WM_SIZE / SIZE_MINIMIZED
        if (msg == CallbackMessage)
        {
            uint action = (uint)((long)lp & 0xffff);
            if (action is 0x400 or 0x401) _dispatch(1);
            else if (action == 0x7b)
            {
                nint menu = CreatePopupMenu();
                if (menu == 0) return 0;
                try
                {
                    AppendMenuW(menu, 0, 1, "Fish öffnen");
                    AppendMenuW(menu, Running ? 1u : 0u, 2, "Starten");
                    AppendMenuW(menu, Running ? 0u : 1u, 3, "Stoppen");
                    AppendMenuW(menu, 0x800, 0, ""); AppendMenuW(menu, 0, 4, "Beenden");
                    GetCursorPos(out var point); Desktop.SetForegroundWindow(window);
                    uint command = TrackPopupMenu(menu, 0x100 | 2, point.X, point.Y, 0, window, 0);
                    Desktop.PostMessageW(window, 0, 0, 0);
                    if (command != 0) _dispatch((int)command);
                }
                finally { DestroyMenu(menu); }
            }
            return 0;
        }
        return DefSubclassProc(window, msg, wp, lp);
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shell_NotifyIconW(2, ref _data); RemoveWindowSubclass(_window, _proc, 1);
        if (_ownsIcon) DestroyIcon(_icon);
        Available = false;
    }
}
