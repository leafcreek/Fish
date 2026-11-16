using Fish.Native;
using Fish.Services;

static class DesktopTests
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetGuiResources(nint process, int flags);
    public static void Run(Action<bool, string> check, string[] args)
    {
        if (args.Contains("--desktop-smoke"))
        {
            var monitors = Desktop.Monitors();
            check(monitors.Length > 0, "Native Monitor-Erkennung");
            var bounds = monitors[0].Bounds;
            using var desktop = new BotDesktop();
            var frame = desktop.Capture(new(bounds.X, bounds.Y, 2, 2));
            check(frame.Width == 2 && frame.Height == 2 && frame.Pixels.Length == 4, "Native Bildschirmaufnahme mit vollständigem Pixelpuffer");
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            int beforeCapture = GetGuiResources(process.Handle, 0);
            using (var capture = new ScreenCapture())
            {
                var retained = capture.Capture(new(bounds.X, bounds.Y, 10, 10));
                var savedPixels = (int[])retained.Pixels.Clone();
                int steady = GetGuiResources(process.Handle, 0);
                for (int i = 0; i < 100; i++) capture.Capture(new(bounds.X, bounds.Y, 10, 10));
                check(GetGuiResources(process.Handle, 0) == steady, "Wiederholte Aufnahmen erzeugen keine zusätzlichen GDI-Ressourcen");
                capture.Capture(new(bounds.X, bounds.Y, 20, 15));
                check(retained.Pixels.SequenceEqual(savedPixels), "Pufferwechsel verändert keine zuvor übergebenen Bilder");
            }
            check(GetGuiResources(process.Handle, 0) == beforeCapture, "Aufnahme gibt ihren wiederverwendeten DIB und DC vollständig frei");
            using var overlay = new OverlayWindow(0x0000ff);
            overlay.Show(new(bounds.X + 10, bounds.Y + 10, 40, 40)); overlay.Hide();
            check(true, "Native Overlay-Erzeugung, Region, Anzeigen und Freigabe");
            using var window = new TestWindow();
            Desktop.ShowWindow(window.Handle, 4);
            check(Desktop.GetForegroundWindow() != window.Handle && desktop.IsAvailable(window.Handle),
                "Sichtbares inaktives Fenster ist nativ verfügbar");
            var before = desktop.GetGameBounds(window.Handle) ?? throw new Exception("Client-Bereich fehlt.");
            Desktop.SetWindowPos(window.Handle, 0, 300, 200, 1000, 700, 0x0014);
            var after = desktop.GetGameBounds(window.Handle) ?? throw new Exception("Client-Bereich fehlt nach Verschieben.");
            check(after.X - before.X == 200 && after.Y - before.Y == 100 && after.Width - before.Width == 200 && after.Height - before.Height == 100,
                "Native Client-Koordinaten folgen Verschieben und Größenänderung");
            Desktop.ShowWindow(window.Handle, 6);
            check(desktop.GetGameBounds(window.Handle) == null, "Minimierte Fenster liefern keinen Aufnahmebereich");
        }
    }
}

sealed class TestWindow : IDisposable
{
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern nint CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint data);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyWindow(nint handle);
    public nint Handle { get; }
    public nint FocusTarget { get; }
    public TestWindow(bool white = false)
    {
        Handle = CreateWindowExW(white ? 0x80u : 0x08000080u, "STATIC", "Fish geometry test", 0x00cf0000u, 100, 100, 800, 600, 0, 0, 0, 0);
        if (Handle == 0) throw new Exception("Testfenster konnte nicht erstellt werden.");
        if (white) FocusTarget = CreateWindowExW(0, "BUTTON", "Overlay Testfläche", 0x50010000u, 0, 0, 1100, 800, Handle, 0, 0, 0);
    }
    public void Dispose() => DestroyWindow(Handle);
}
