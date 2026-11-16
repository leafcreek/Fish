# Opt-in desktop regression: drag only handles owned by this smoke test's Fish process.
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
public static class FishSettingsOverlayInput {
    private delegate bool EnumProc(IntPtr window, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct GuiInfo {
        public uint Size, Flags;
        public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public Rect CaretRect;
    }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    private static void MouseAt(int x, int y, uint button) {
        uint nx = (uint)Math.Max(0, Math.Min(65535, (int)((x - GetSystemMetrics(76) + .5) * 65536 / GetSystemMetrics(78))));
        uint ny = (uint)Math.Max(0, Math.Min(65535, (int)((y - GetSystemMetrics(77) + .5) * 65536 / GetSystemMetrics(79))));
        mouse_event(0x8000 | 0x4000 | 1 | button, nx, ny, 0, UIntPtr.Zero);
    }
    private static IntPtr FindHandle(uint process, string title) {
        IntPtr handle = IntPtr.Zero;
        EnumWindows((candidate, data) => {
            uint owner; GetWindowThreadProcessId(candidate, out owner);
            if (owner != process || !IsWindowVisible(candidate)) return true;
            var text = new StringBuilder(128); GetWindowTextW(candidate, text, text.Capacity);
            if (text.ToString() != title) return true;
            handle = candidate; return false;
        }, IntPtr.Zero);
        if (handle == IntPtr.Zero) throw new InvalidOperationException("Test Fish overlay handle not visible: " + title);
        return handle;
    }
    public static Rect Bounds(uint process, string title) {
        IntPtr previousDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try {
            Rect rect;
            if (!GetWindowRect(FindHandle(process, title), out rect)) throw new InvalidOperationException("Own grip bounds unavailable.");
            return rect;
        } finally { if (previousDpi != IntPtr.Zero) SetThreadDpiAwarenessContext(previousDpi); }
    }
    public static void Drag(uint process, string title, int dx, int dy, Action duringDrag) {
        IntPtr handle = FindHandle(process, title);
        if (((GetAsyncKeyState(1) | GetAsyncKeyState(2)) & 0x8000) != 0) throw new InvalidOperationException("Release mouse buttons before desktop test.");
        IntPtr previousDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
        Rect rect; GetWindowRect(handle, out rect);
        int x = (rect.Left + rect.Right) / 2, y = (rect.Top + rect.Bottom) / 2;
        Point original; GetCursorPos(out original);
        bool down = false;
        try {
            SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x53);
            SetCursorPos(x, y); Thread.Sleep(100);
            if (WindowFromPoint(new Point { X = x, Y = y }) != handle) throw new InvalidOperationException("Own test grip is obscured; no input sent.");
            MouseAt(x, y, 2); down = true; Thread.Sleep(150);
            uint owner; uint thread = GetWindowThreadProcessId(handle, out owner);
            var info = new GuiInfo { Size = (uint)Marshal.SizeOf(typeof(GuiInfo)) };
            if (!GetGUIThreadInfo(thread, ref info) || info.Capture != handle) throw new InvalidOperationException("Own test grip did not capture mouse.");
            MouseAt(x + dx, y + dy, 0); Thread.Sleep(200);
            duringDrag();
        } finally {
            if (down) { MouseAt(x + dx, y + dy, 4); Thread.Sleep(200); }
            SetCursorPos(original.X, original.Y);
            if (previousDpi != IntPtr.Zero) SetThreadDpiAwarenessContext(previousDpi);
        }
    }
}
'@

function Test-SettingsOverlay($window, [uint32]$processId) {
    function Element([string]$id) {
        $found = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id))
        if (!$found) { throw "Settings control missing: $id" }
        return $found
    }
    $scroll = (Element 'MainScroll').GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    if ($scroll.Current.VerticallyScrollable) { for ($step=0; $step -lt 8 -and $scroll.Current.VerticalScrollPercent -gt 0; $step++) { $scroll.Scroll([System.Windows.Automation.ScrollAmount]::NoAmount,[System.Windows.Automation.ScrollAmount]::LargeDecrement); Start-Sleep -Milliseconds 100 } }
    Start-Sleep -Milliseconds 400
    $scrollBefore = $scroll.Current.VerticalScrollPercent
    $ids = @('BobberSensitivity','SplashSensitivity','FishingHotkey')
    $controls = @{}
    $runtimeIds = @{}
    foreach ($id in $ids) { $controls[$id] = Element $id; $runtimeIds[$id] = $controls[$id].GetRuntimeId() -join ',' }
    function Region {
        $move = [FishSettingsOverlayInput]::Bounds($processId, 'Schwimmer-Bereich verschieben')
        $resize = [FishSettingsOverlayInput]::Bounds($processId, 'Schwimmer-Bereich skalieren')
        return [pscustomobject]@{ Width = $resize.Left-$move.Left; Height = $resize.Top-$move.Top;
            X = $move.Left; Y = $move.Top }
    }
    function Assert-StableSettings {
        foreach ($id in $ids) {
            if (($controls[$id].GetRuntimeId() -join ',') -ne $runtimeIds[$id]) { throw "Settings control was recreated: $id" }
        }
        if ([Math]::Abs($scroll.Current.VerticalScrollPercent - $scrollBefore) -gt 0.1) { throw 'Overlay editing reset settings scroll position.' }
    }
    $before = Region
    [FishSettingsOverlayInput]::Drag($processId, 'Schwimmer-Bereich verschieben', 24, 16, [Action]{
        Assert-StableSettings
        $actual = Region
        if ($actual.X -eq $before.X -and $actual.Y -eq $before.Y) { throw 'Drag did not move the bobber frame.' }
    })
    Assert-StableSettings
    $before = Region
    [FishSettingsOverlayInput]::Drag($processId, 'Schwimmer-Bereich skalieren', 32, 24, [Action]{
        Assert-StableSettings
        $actual = Region
        if ($actual.Width -eq $before.Width -and $actual.Height -eq $before.Height) { throw 'Resize did not change the bobber frame.' }
    })
    Assert-StableSettings
    Write-Output 'PASS mouse grips move/resize the bobber frame without rebuilding settings'
}
