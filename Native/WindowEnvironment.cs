using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Fish.Native;

// Keep display/DPI handling independent of notification-area icons.
internal sealed class WindowEnvironment : IDisposable
{
    private delegate nint SubclassProc(nint window, uint message, nint wp, nint lp, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint window, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint window, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint window, uint message, nint wp, nint lp);
    private readonly nint _window;
    private readonly SubclassProc _proc;
    private readonly Action _changed;
    private bool _disposed;

    public WindowEnvironment(nint window, Action changed)
    {
        _window = window; _changed = changed; _proc = Handle;
        if (!SetWindowSubclass(window, _proc, 1, 0)) throw new Win32Exception("Bildschirmüberwachung konnte nicht angelegt werden.");
    }
    private nint Handle(nint window, uint message, nint wp, nint lp, nuint id, nuint data)
    {
        if (!_disposed && message is 0x007e or 0x02e0) _changed();
        return DefSubclassProc(window, message, wp, lp);
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RemoveWindowSubclass(_window, _proc, 1);
    }
}
