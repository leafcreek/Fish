using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Fish.Native;

internal static class WindowZOrder
{
    [DllImport("user32.dll")] private static extern int GetWindowLongW(nint window, int index);
    public static void EnsureTopmost(nint window, bool enabled)
    {
        bool actual = (GetWindowLongW(window, -20) & 8) != 0;
        if (actual == enabled) return;
        if (!Desktop.SetWindowPos(window, enabled ? -1 : -2, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Fensterreihenfolge konnte nicht geändert werden.");
    }
}
