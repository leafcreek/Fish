param([Parameter(Mandatory=$true)][string]$Executable, [switch]$BesideExecutable)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FishSmokeNative {
    public delegate bool EnumProc(IntPtr window, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr window, System.Text.StringBuilder name, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowW(string cls, string title);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int w, int h, uint flags);
    public static Rect[] Overlays(uint process) {
        var result = new System.Collections.Generic.List<Rect>();
        EnumWindows((window, data) => {
            uint owner; GetWindowThreadProcessId(window, out owner);
            var name = new System.Text.StringBuilder(256); GetClassNameW(window, name, 256);
            Rect rect;
            if (owner == process && name.ToString().StartsWith("FishOverlay_") && IsWindowVisible(window) && GetWindowRect(window, out rect)) result.Add(rect);
            return true;
        }, IntPtr.Zero);
        return result.ToArray();
    }
    [DllImport("user32.dll")] public static extern int GetWindowLongW(IntPtr window, int index);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr data);
}
'@
$settings = Join-Path (Get-Location) ('artifacts\ui-smoke-' + [Guid]::NewGuid().ToString('N'))
$launch = @{ FilePath = (Resolve-Path -LiteralPath $Executable).Path; WindowStyle = 'Hidden'; PassThru = $true }
if ($BesideExecutable) {
    $settings = Split-Path -Parent $launch.FilePath
    if ((Test-Path -LiteralPath (Join-Path $settings 'appsettings.json')) -or (Test-Path -LiteralPath (Join-Path $settings 'appsettings.json.bak'))) {
        throw 'Existing settings found next to EXE; refusing to overwrite them.'
    }
} else { $launch.ArgumentList = @('--settings-directory', $settings) }
$process = Start-Process @launch
try {
    $window = $null
    for ($i = 0; $i -lt 80; $i++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Fish exited during startup: $($process.ExitCode)" }
        if ($process.MainWindowHandle -ne 0) {
            $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
            if ($null -ne $window) { break }
        }
    }
    if ($null -eq $window) { throw 'Fish window not found.' }
    function Find-Element([string]$name, [switch]$ById) {
        $property = if ($ById) { [System.Windows.Automation.AutomationElement]::AutomationIdProperty } else { [System.Windows.Automation.AutomationElement]::NameProperty }
        $condition = [System.Windows.Automation.PropertyCondition]::new($property, $name)
        for ($attempt = 0; $attempt -lt 25; $attempt++) {
            $element = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            if ($null -ne $element) { return $element }
            Start-Sleep -Milliseconds 200
        }
        throw "Element not found: $name"
    }
    Find-Element 'Starten' | Out-Null
    $start = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'StartButton'))
    if (!$start.Current.IsEnabled) { throw 'Start button should be enabled.' }
    $stop = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'StopButton'))
    if ($stop.Current.IsEnabled) { throw 'Stop button should be disabled before starting.' }
    Write-Output 'PASS startup and initial control states'
    Start-Sleep -Milliseconds 1000
    $process.Refresh()
    $style = [FishSmokeNative]::GetWindowLongW($process.MainWindowHandle, -20)
    if (($style -band 8) -eq 0) { throw 'Fish is not a native topmost window.' }
    $competitor = [FishSmokeNative]::CreateWindowExW(0x80, 'STATIC', 'Fish topmost test', 0x10cf0000, 100, 100, 400, 300, 0, 0, 0, 0)
    if ($competitor -eq [IntPtr]::Zero) { throw 'Could not create test window.' }
    try {
        [FishSmokeNative]::SetForegroundWindow($competitor) | Out-Null
        Start-Sleep -Milliseconds 250
        $previous = [FishSmokeNative]::GetWindow($process.MainWindowHandle, 3)
        while ($previous -ne [IntPtr]::Zero) {
            if ($previous -eq $competitor) { throw 'Normal test window is above Fish.' }
            $previous = [FishSmokeNative]::GetWindow($previous, 3)
        }
    } finally { [FishSmokeNative]::DestroyWindow($competitor) | Out-Null }
    $pattern = $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Minimized)
    $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
    Start-Sleep -Milliseconds 250
    if (([FishSmokeNative]::GetWindowLongW($process.MainWindowHandle, -20) -band 8) -eq 0) { throw 'Topmost lost after restore.' }
    Write-Output 'PASS native topmost, competing window and restore'
    if ([FishSmokeNative]::FindWindowW($null, 'World of Warcraft') -eq [IntPtr]::Zero) {
        $game = [FishSmokeNative]::CreateWindowExW(0x08000080, 'STATIC', 'World of Warcraft', 0x10cf0000, 100, 100, 1000, 700, 0, 0, 0, 0)
        if ($game -eq [IntPtr]::Zero) { throw 'Could not create overlay test window.' }
        try {
            Start-Sleep -Milliseconds 600
            $before = @([FishSmokeNative]::Overlays($process.Id) | Sort-Object Left)
            if ($before.Count -ne 2) { throw 'Both overlays must be visible before starting.' }
            [FishSmokeNative]::SetWindowPos($game, 0, 200, 200, 1000, 700, 0x0014) | Out-Null
            Start-Sleep -Milliseconds 600
            $after = @([FishSmokeNative]::Overlays($process.Id) | Sort-Object Left)
            if ($after.Count -ne 2 -or $after[0].Left -ne ($before[0].Left + 100) -or $after[1].Top -ne ($before[1].Top + 100)) { throw 'Idle overlays did not follow the game window.' }
            $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Minimized)
            Start-Sleep -Milliseconds 400
            if (@([FishSmokeNative]::Overlays($process.Id)).Count -ne 2) { throw 'Minimizing Fish hid overlays.' }
            $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
            [FishSmokeNative]::ShowWindow($game, 6) | Out-Null
            Start-Sleep -Milliseconds 400
            if (@([FishSmokeNative]::Overlays($process.Id)).Count -ne 0) { throw 'Minimized game retained overlays.' }
            [FishSmokeNative]::ShowWindow($game, 4) | Out-Null
            Start-Sleep -Milliseconds 400
            if (@([FishSmokeNative]::Overlays($process.Id)).Count -ne 2) { throw 'Restoring game did not restore overlays.' }
            Write-Output 'PASS idle overlays, window movement, Fish minimization and game restore'
        } finally { [FishSmokeNative]::DestroyWindow($game) | Out-Null }
    } else { Write-Output 'SKIP synthetic overlay window: WoW is already open.' }
    (Find-Element 'Erkennung').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 300
    $minimum = Find-Element 'BobberMatchMinimum' -ById
    $inputCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
    $minimumInput = $minimum.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $inputCondition)
    $minimumInput.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('10')
    (Find-Element 'SplashSettings' -ById).GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $splashMinimum = Find-Element 'SplashChangePercent' -ById
    $splashInput = $splashMinimum.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $inputCondition)
    $splashInput.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('7')
    (Find-Element 'Aktionen').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (!$process.WaitForExit(10000)) { throw 'Fish did not close cleanly.' }
    if ($process.ExitCode -ne 0) { throw "Fish exited with code $($process.ExitCode)" }
    Write-Output 'PASS orderly window close'
    $settingsFile = Join-Path $settings 'appsettings.json'
    if (!(Test-Path -LiteralPath $settingsFile)) { throw 'Settings were not saved on close.' }
    $saved = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
    if ($saved.BobberMatchMinimum -ne 10) { throw 'Changed setting was not saved.' }
    if ($saved.SplashChangePercent -ne 7 -or $saved.SplashPixelTolerance -ne 20 -or $saved.SplashCalibrationMilliseconds -ne 500) { throw 'Motion detection settings were not saved.' }
    Write-Output 'PASS automatic save on close'
    $process.Dispose()
    $process = Start-Process @launch
    $window = $null
    for ($i = 0; $i -lt 80; $i++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw 'Fish exited during restart.' }
        if ($process.MainWindowHandle -ne 0) {
            $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
            if ($null -ne $window) { break }
        }
    }
    if ($null -eq $window) { throw 'Restarted Fish window not found.' }
    Start-Sleep -Milliseconds 1000
    (Find-Element 'Erkennung').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 300
    $minimum = Find-Element 'BobberMatchMinimum' -ById
    $minimumInput = $minimum.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $inputCondition)
    if ($minimumInput.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -ne '10') { throw 'Saved setting was not restored.' }
    (Find-Element 'SplashSettings' -ById).GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $splashMinimum = Find-Element 'SplashChangePercent' -ById
    $splashInput = $splashMinimum.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $inputCondition)
    if ($splashInput.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -ne '7') { throw 'Motion detection setting was not restored.' }
    if (([FishSmokeNative]::GetWindowLongW($process.MainWindowHandle, -20) -band 8) -eq 0) { throw 'Restarted window is not topmost.' }
    Write-Output 'PASS automatic reload on restart'
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (!$process.WaitForExit(10000) -or $process.ExitCode -ne 0) { throw 'Restarted Fish did not close cleanly.' }
    Write-Output "Settings: $settings"
}
finally {
    if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }
    $process.Dispose()
    if ($BesideExecutable) {
        foreach ($name in @('appsettings.json', 'appsettings.json.bak')) {
            $path = Join-Path $settings $name
            if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
        }
    }
}
