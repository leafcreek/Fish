using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Fish.Configuration;
using Fish.Core;
using Fish.Native;

static class OverlayDesktopTests
{
    private delegate bool EnumProc(nint window, nint data);
    [StructLayout(LayoutKind.Sequential)] private struct Message
    {
        public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public Desktop.Point Point; public uint Private;
    }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, nint data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(nint window, StringBuilder text, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(nint window, StringBuilder text, int size);
    [DllImport("user32.dll")] private static extern int GetWindowLongW(nint window, int index);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Desktop.Rect rect);
    [DllImport("user32.dll")] private static extern bool PeekMessageW(out Message message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(ref Message message);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Desktop.Point point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Desktop.Point point);
    [DllImport("user32.dll")] private static extern nint GetCapture();
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, nuint extra);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint SendMessageW(nint window, uint message, nint wp, nint lp);
    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool AttachThreadInput(uint first, uint second, bool attach);
    [DllImport("user32.dll")] private static extern nint SetFocus(nint window);
    [DllImport("user32.dll")] private static extern nint SetActiveWindow(nint window);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();

    public static void Run(Action<bool, string> check)
    {
        var released = Stopwatch.StartNew();
        while (((GetAsyncKeyState(1) | GetAsyncKeyState(2)) & 0x8000) != 0 && released.ElapsedMilliseconds < 5000) Thread.Sleep(20);
        if ((GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0)
            throw new InvalidOperationException($"Mausprüfung benötigt losgelassene Maustasten (links={GetAsyncKeyState(1)}, rechts={GetAsyncKeyState(2)}).");
        nint previousDpi = SetThreadDpiAwarenessContext(-4);
        nint previousForeground = Desktop.GetForegroundWindow();
        GetCursorPos(out var previousCursor);
        try
        {
            using var window = new TestWindow(white: true);
            Desktop.SetWindowPos(window.Handle, -1, 100, 100, 1000, 700, 0x0010 | 0x0040);
            FocusFixture(window.Handle, window.FocusTarget);
            Pump(); DwmFlush();
            var client = GameWindow.Bounds(window.Handle)!.Value;
            var options = new BotOptions { BobberAreaWidth = 200, BobberAreaHeight = 160, BobberAreaYOffset = 0 };
            var initial = new SearchRegion(200, 160, 0, 0).In(client);
            using var capture = new ScreenCapture();
            var sampleArea = new PixelRect(initial.X + 2, initial.Y + 2, 16, 16);
            var before = capture.Capture(sampleArea);
            using var overlays = new DetectionOverlays(new FixedWindow(window.Handle));
            var editing = new List<bool>();
            var errors = new List<Exception>();
            int completed = 0;
            overlays.EditingChanged += editing.Add;
            overlays.InteractionFailed += errors.Add;
            overlays.BobberRegionChanged += (region, done) =>
            {
                options.BobberAreaWidth = region.Width; options.BobberAreaHeight = region.Height;
                options.BobberAreaXOffset = region.XOffset; options.BobberAreaYOffset = region.YOffset;
                if (done) completed++;
            };
            overlays.Update(options, null, false); Pump(); DwmFlush();
            nint move = FindHandle("Schwimmer-Bereich verschieben"), resize = FindHandle("Schwimmer-Bereich skalieren");
            check(move != 0 && resize != 0, "Schwimmer-Rahmen besitzt zwei eigene, erreichbare Mausgriffe");
            var withHandles = capture.Capture(sampleArea);
            check(withHandles.Pixels.SequenceEqual(before.Pixels), "Sichtbarer Griff erscheint nicht in den Pixeln für die Erkennung");

            Drag(move, 45, 30);
            check(options.BobberAreaXOffset == 45 && options.BobberAreaYOffset == -30 &&
                options.BobberAreaWidth == 200 && options.BobberAreaHeight == 160 && completed == 1,
                $"Echte Mausbewegung verschiebt den Suchbereich und übernimmt seine Versätze ({options.BobberAreaXOffset},{options.BobberAreaYOffset}; {options.BobberAreaWidth}x{options.BobberAreaHeight}; Abschlüsse {completed})");
            check(editing.SequenceEqual([true, false]) && !overlays.IsEditing && GetCapture() == 0,
                "Bearbeitung meldet Pause und Fortsetzung und gibt die Maus wieder frei");

            overlays.Update(options, null, false); Pump();
            Drag(resize, 60, 40);
            check(options.BobberAreaWidth == 260 && options.BobberAreaHeight == 200 && completed == 2,
                $"Echte Mausbewegung am Eckgriff vergrößert beide Dimensionen ({options.BobberAreaWidth}x{options.BobberAreaHeight}; Abschlüsse {completed}; Bearbeitung {string.Join(',', editing)}; Fehler {string.Join(';', errors.Select(error => error.Message))})");
            var saved = new SearchRegion(options.BobberAreaWidth, options.BobberAreaHeight, options.BobberAreaXOffset, options.BobberAreaYOffset);
            Drag(move, 25, 20, cancel: true);
            check(new SearchRegion(options.BobberAreaWidth, options.BobberAreaHeight, options.BobberAreaXOffset, options.BobberAreaYOffset) == saved &&
                !overlays.IsEditing && GetCapture() == 0, "Escape setzt eine Mausänderung zurück und beendet die Bearbeitung");

            overlays.Update(options, null, false); Pump();
            Drag(move, 15, 10, duringDrag: () =>
            {
                Desktop.SetWindowPos(window.Handle, -1, 120, 120, 1000, 700, 0x0010 | 0x0040);
                overlays.Update(options, null, false);
            });
            check(new SearchRegion(options.BobberAreaWidth, options.BobberAreaHeight, options.BobberAreaXOffset, options.BobberAreaYOffset) == saved &&
                !overlays.IsEditing, "Eine bewegte Spielfläche bricht die Griffbearbeitung ohne falsche Versätze ab");
            check(FindHandle("Spritzer-Bereich verschieben") == 0, "Ohne Schwimmer gibt es keine Spritzer-Griffe");
            client = GameWindow.Bounds(window.Handle)!.Value;
            var bobber = new PixelPoint(client.X + client.Width / 2 + 60, client.Y + client.Height / 2);
            BotUpdate FishingUpdate() => new(BotState.Fishing, "Test", saved.In(client),
                FishingDefaults.SplashRegion.Around(bobber, client),
                window.Handle, bobber);
            overlays.Update(options, FishingUpdate(), true); Pump();
            nint splashFrame = FindFrame(FishingUpdate().SplashArea!.Value);
            check(splashFrame != 0, "Gefundener Schwimmer zeigt weiterhin den Spritzer-Rahmen");
            check(FindHandle("Spritzer-Bereich verschieben") == 0 && FindHandle("Spritzer-Bereich skalieren") == 0,
                "Auch der sichtbare Spritzer-Rahmen besitzt keine Mausgriffe mehr");
            check((GetWindowLongW(splashFrame, -20) & 0x08000020) == 0x08000020,
                "Spritzer-Rahmen bleibt durchklickbar und übernimmt keinen Fokus");
            int editsBefore = editing.Count;
            overlays.Update(options, FishingUpdate(), true); Pump();
            check(FishingUpdate().SplashArea is { Width: 50, Height: 50 } && FindFrame(FishingUpdate().SplashArea!.Value) == splashFrame &&
                !overlays.IsEditing && editing.Count == editsBefore, "Spritzer-Rahmen bleibt unveränderlich 50 × 50 und mittig am Bobber");
            overlays.Update(options, null, false); Pump();
            check(!Desktop.IsWindowVisible(splashFrame), "Stop versteckt den Spritzer-Rahmen");
            check(errors.Count == 0, "Native Mausbehandlung wirft keine Fensterprozedurfehler");
        }
        finally
        {
            SetCursorPos(previousCursor.X, previousCursor.Y);
            if (previousForeground != 0 && Desktop.IsWindow(previousForeground)) Desktop.SetForegroundWindow(previousForeground);
            if (previousDpi != 0) SetThreadDpiAwarenessContext(previousDpi);
        }
    }

    private static void Drag(nint handle, int dx, int dy, bool cancel = false, Action? duringDrag = null)
    {
        GetWindowRect(handle, out var rect);
        int x = (rect.Left + rect.Right) / 2, y = (rect.Top + rect.Bottom) / 2;
        Desktop.SetWindowPos(handle, -1, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
        SetCursorPos(x, y); Pump();
        if (WindowFromPoint(new() { X = x, Y = y }) != handle) throw new Exception("Testgriff ist durch ein anderes Fenster verdeckt.");
        MouseAt(x, y, 0x02); Pump();
        try
        {
            if (GetCapture() != handle)
            {
                GetCursorPos(out var actual);
                var hit = SendMessageW(handle, 0x84, 0, (actual.Y << 16) | (actual.X & 0xffff));
                throw new Exception($"Testgriff hat keine Mausaufnahme erhalten: capture={GetCapture()}, target={handle}, foreground={Desktop.GetForegroundWindow()}, left={GetAsyncKeyState(1)}, pointer={actual.X},{actual.Y}, expected={x},{y}, hit={hit}.");
            }
            MouseAt(x + dx, y + dy, 0); Pump();
            duringDrag?.Invoke();
            if (cancel) { Desktop.PostMessageW(handle, 0x100, 0x1b, 0); Pump(); }
        }
        finally { MouseAt(x + dx, y + dy, 0x04); Pump(); }
    }
    private static void MouseAt(int x, int y, uint button)
    {
        int left = GetSystemMetrics(76), top = GetSystemMetrics(77), width = GetSystemMetrics(78), height = GetSystemMetrics(79);
        uint nx = (uint)Math.Clamp((int)((x - left + .5) * 65536.0 / Math.Max(1, width)), 0, 65535);
        uint ny = (uint)Math.Clamp((int)((y - top + .5) * 65536.0 / Math.Max(1, height)), 0, 65535);
        mouse_event(0x8000 | 0x4000 | 0x0001 | button, nx, ny, 0, 0);
    }

    private static nint FindFrame(PixelRect area)
    {
        nint found = 0;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint process);
            if (process != Environment.ProcessId || !Desktop.IsWindowVisible(window)) return true;
            var name = new StringBuilder(128); GetClassNameW(window, name, name.Capacity);
            if (!name.ToString().StartsWith("FishOverlay_")) return true;
            GetWindowRect(window, out var rect);
            if (rect.Left == area.X - 2 && rect.Top == area.Y - 2 && rect.Right == area.X + area.Width + 2 && rect.Bottom == area.Y + area.Height + 2)
            { found = window; return false; }
            return true;
        }, 0);
        return found;
    }
    private static nint FindHandle(string title)
    {
        nint found = 0;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint process);
            if (process != Environment.ProcessId) return true;
            var text = new StringBuilder(128); GetWindowTextW(window, text, text.Capacity);
            if (text.ToString() != title) return true;
            found = window; return false;
        }, 0);
        return found;
    }
    private static void Pump()
    {
        var timer = Stopwatch.StartNew();
        do
        {
            while (PeekMessageW(out var message, 0, 0, 0, 1)) { TranslateMessage(ref message); DispatchMessageW(ref message); }
            Thread.Sleep(1);
        } while (timer.ElapsedMilliseconds < 50);
    }
    private static void FocusFixture(nint handle, nint target)
    {
        // Attach input queues only while activating our fixture; never inject a click to gain focus.
        for (int attempt = 0; attempt < 10 && Desktop.GetForegroundWindow() != handle; attempt++)
        {
            uint current = GetCurrentThreadId();
            uint foreground = GetWindowThreadProcessId(Desktop.GetForegroundWindow(), out _);
            bool attached = foreground != 0 && foreground != current && AttachThreadInput(current, foreground, true);
            try
            {
                SetActiveWindow(handle); Desktop.SetForegroundWindow(handle); SetFocus(target); Pump();
            }
            finally { if (attached) AttachThreadInput(current, foreground, false); }
            Pump();
        }
        if (Desktop.GetForegroundWindow() != handle) throw new Exception("Testfenster hat keinen Vordergrundfokus; keine Mauseingaben gesendet.");
    }
    private sealed class FixedWindow(nint window) : IGameWindow
    {
        public nint FindGame() => window;
        public bool IsAvailable(nint game) => game == window && GameWindow.IsAvailable(game);
        public PixelRect? GetGameBounds(nint game) => game == window ? GameWindow.Bounds(game) : null;
    }
}
