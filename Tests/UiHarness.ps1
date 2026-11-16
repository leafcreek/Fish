Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class FishUiWindow {
    private delegate bool EnumProc(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    public static IntPtr Find(uint process) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, data) => {
            uint owner; GetWindowThreadProcessId(window, out owner);
            if (owner != process) return true;
            var text = new StringBuilder(256); GetWindowTextW(window, text, text.Capacity);
            if (text.ToString() != "Fish") return true;
            found = window; return false;
        }, IntPtr.Zero);
        return found;
    }
}
'@

function Wait-FishWindow([System.Diagnostics.Process]$App) {
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        $App.Refresh()
        if ($App.HasExited) { throw "Fish exited during startup: $($App.ExitCode)" }
        $handle = [FishUiWindow]::Find($App.Id)
        if ($handle -ne [IntPtr]::Zero) {
            [FishUiWindow]::ShowWindow($handle, 5) | Out-Null
            return [System.Windows.Automation.AutomationElement]::FromHandle($handle)
        }
        Start-Sleep -Milliseconds 250
    }
    throw 'Fish main window was not available within 20 seconds.'
}
