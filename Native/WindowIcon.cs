using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Fish.Native;

internal sealed class WindowIcon : IDisposable
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint ExtractIconExW(string file, int index, out nint large, out nint small, uint count);
    [DllImport("user32.dll")] private static extern nint SendMessageW(nint window, uint message, nint wp, nint lp);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    private readonly nint _window, _large, _small;
    private bool _disposed;

    public WindowIcon(nint window)
    {
        _window = window;
        if (Environment.ProcessPath is not { } executable || ExtractIconExW(executable, 0, out _large, out _small, 1) == 0 ||
            _large == 0 && _small == 0)
            throw new Win32Exception("Das Goldfisch-Fenstersymbol konnte nicht geladen werden.");
        SendMessageW(window, 0x0080, 1, _large != 0 ? _large : _small);
        SendMessageW(window, 0x0080, 0, _small != 0 ? _small : _large);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SendMessageW(_window, 0x0080, 0, 0);
        SendMessageW(_window, 0x0080, 1, 0);
        if (_small != 0 && _small != _large) DestroyIcon(_small);
        if (_large != 0) DestroyIcon(_large);
    }
}
