param([Parameter(Mandatory=$true)][string]$Executable, [switch]$BesideExecutable, [switch]$NoSession, [switch]$NativeInput, [switch]$NativeOverlay)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UiHarness.ps1')
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
    public static IntPtr FindGame() { return FindWindowW(null, "World of Warcraft"); }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr window, System.Text.StringBuilder text, int size);
    public static string[] WindowSummary(uint process) {
        var entries = new System.Collections.Generic.List<string>();
        EnumWindows((window, data) => {
            uint owner; GetWindowThreadProcessId(window, out owner);
            if (owner != process) return true;
            var title = new System.Text.StringBuilder(256); GetWindowTextW(window, title, title.Capacity);
            var cls = new System.Text.StringBuilder(256); GetClassNameW(window, cls, cls.Capacity);
            entries.Add(window + " " + cls + " title=" + title + " style=" + GetWindowLongW(window, -20)); return true;
        }, IntPtr.Zero);
        return entries.ToArray();
    }
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr window, uint message, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int w, int h, uint flags);
    public static Rect[] Overlays(uint process) {
        return Overlays(process, false);
    }
    public static Rect[] Overlays(uint process, bool includeHandles) {
        var result = new System.Collections.Generic.List<Rect>();
        EnumWindows((window, data) => {
            uint owner; GetWindowThreadProcessId(window, out owner);
            var name = new System.Text.StringBuilder(256); GetClassNameW(window, name, 256);
            Rect rect;
            if (owner == process && name.ToString().StartsWith(includeHandles ? "FishOverlay" : "FishOverlay_") && IsWindowVisible(window) && GetWindowRect(window, out rect)) result.Add(rect);
            return true;
        }, IntPtr.Zero);
        return result.ToArray();
    }
    [DllImport("user32.dll")] public static extern int GetWindowLongW(IntPtr window, int index);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    public static void TapKey(IntPtr expectedWindow, byte key, bool control) {
        if (GetForegroundWindow() != expectedWindow) throw new InvalidOperationException("Test window lost keyboard focus.");
        if (control) keybd_event(0x11, 0, 0, UIntPtr.Zero);
        try {
            keybd_event(key, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, 2, UIntPtr.Zero);
        } finally { if (control) keybd_event(0x11, 0, 2, UIntPtr.Zero); }
    }
    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr data);
}
'@
[FishSmokeNative]::SetThreadDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null
$settings = Join-Path (Get-Location) ('artifacts\ui-tests\settings-' + [Guid]::NewGuid().ToString('N'))
$launch = @{ FilePath = (Resolve-Path -LiteralPath $Executable).Path; WindowStyle = 'Hidden'; PassThru = $true }
if ($BesideExecutable) {
    $settings = Split-Path -Parent $launch.FilePath
    if ((Test-Path -LiteralPath (Join-Path $settings 'settings.json')) -or (Test-Path -LiteralPath (Join-Path $settings 'settings.json.bak'))) {
        throw 'Existing settings found next to EXE; refusing to overwrite them.'
    }
}
$launch.ArgumentList = @('--settings-directory', $settings)
$process = Start-Process @launch
try {
    $window = Wait-FishWindow $process
    $mainHandle = [FishUiWindow]::Find($process.Id)
    function Find-Element([string]$name, [switch]$ById) {
        $property = if ($ById) { [System.Windows.Automation.AutomationElement]::AutomationIdProperty } else { [System.Windows.Automation.AutomationElement]::NameProperty }
        $condition = [System.Windows.Automation.PropertyCondition]::new($property, $name)
        for ($attempt = 0; $attempt -lt 50; $attempt++) {
            $process.Refresh()
            if ($process.HasExited) { throw "Fish exited while waiting for $name : $($process.ExitCode)" }
            $element = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            if ($null -ne $element) { return $element }
            Start-Sleep -Milliseconds 200
        }
        $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) | Select-Object -First 40 | ForEach-Object { Write-Output $_.Current.Name }
        throw "Element not found: $name (window: $($window.Current.Name))"
    }
    function Restore-TestWindow {
        # Restore only this test's own window through the taskbar/native path.
        [FishSmokeNative]::ShowWindow($mainHandle, 9) | Out-Null
        [FishSmokeNative]::SetForegroundWindow($mainHandle) | Out-Null
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            Start-Sleep -Milliseconds 100
            if ([FishSmokeNative]::IsWindowVisible($mainHandle)) { break }
        }
    }
    Find-Element 'Starten' | Out-Null
    $start = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'SessionButton'))
    if (!$start.Current.IsEnabled) { throw 'Start button should be enabled.' }
    if ($start.Current.Name -ne 'Starten') { throw 'Session button must initially offer Starten.' }
    Write-Output 'PASS startup and initial control states'
    $stepProgress = Find-Element 'StepTimeProgress' -ById
    $stepTime = Find-Element 'StepTimeText' -ById
    if ($stepProgress.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value -ne 0 -or
        $stepTime.Current.Name -notlike '0,0 / 0,0 s*') { throw 'Step progress did not start idle.' }
    Write-Output 'PASS step progress and elapsed/maximum time appear inside the status card'
    Start-Sleep -Milliseconds 1000
    $process.Refresh()
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        $style = [FishSmokeNative]::GetWindowLongW($mainHandle, -20)
        if (($style -band 8) -ne 0) { break }
        Start-Sleep -Milliseconds 100
    }
    if (($style -band 8) -eq 0) {
        [FishSmokeNative]::WindowSummary($process.Id) | Write-Output
        throw "Fish is not a native topmost window (handle: $mainHandle, style: $style)."
    }
    $competitor = [FishSmokeNative]::CreateWindowExW(0x80, 'STATIC', 'Fish topmost test', 0x10cf0000, 100, 100, 400, 300, 0, 0, 0, 0)
    if ($competitor -eq [IntPtr]::Zero) { throw 'Could not create test window.' }
    try {
        [FishSmokeNative]::SetForegroundWindow($competitor) | Out-Null
        Start-Sleep -Milliseconds 250
        $previous = [FishSmokeNative]::GetWindow($mainHandle, 3)
        while ($previous -ne [IntPtr]::Zero) {
            if ($previous -eq $competitor) { throw 'Normal test window is above Fish.' }
            $previous = [FishSmokeNative]::GetWindow($previous, 3)
        }
    } finally { [FishSmokeNative]::DestroyWindow($competitor) | Out-Null }
    $pattern = $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    $visibleBeforeMinimize = @([FishSmokeNative]::Overlays($process.Id, $true)).Count
    $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Minimized)
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Milliseconds 100
        if (@([FishSmokeNative]::Overlays($process.Id, $true)).Count -eq 0) { break }
    }
    if (@([FishSmokeNative]::Overlays($process.Id, $true)).Count -ne 0) { throw 'Minimizing Fish retained overlays or handles.' }
    if (![FishSmokeNative]::IsWindowVisible($mainHandle)) { throw 'Minimizing Fish hid it in the tray.' }
    $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
    Restore-TestWindow
    Start-Sleep -Milliseconds 250
    if ($visibleBeforeMinimize -gt 0) {
        for ($attempt = 0; $attempt -lt 30 -and @([FishSmokeNative]::Overlays($process.Id, $true)).Count -ne $visibleBeforeMinimize; $attempt++) { Start-Sleep -Milliseconds 100 }
    }
    if (([FishSmokeNative]::GetWindowLongW($mainHandle, -20) -band 8) -eq 0) { throw 'Topmost lost after restore.' }
    if ($visibleBeforeMinimize -gt 0 -and @([FishSmokeNative]::Overlays($process.Id, $true)).Count -ne $visibleBeforeMinimize) { throw 'Restoring Fish did not restore overlays and handles.' }
    Write-Output 'PASS native topmost, competing window and restore'
    Write-Output "PASS overlays and handles hide while Fish is minimized and return after restore ($visibleBeforeMinimize visible before minimize)"
    if ([FishSmokeNative]::FindGame() -eq [IntPtr]::Zero) {
        $game = [FishSmokeNative]::CreateWindowExW(0x08000080, 'STATIC', 'World of Warcraft', 0x10cf0000, 100, 100, 1000, 700, 0, 0, 0, 0)
        if ($game -eq [IntPtr]::Zero) { throw 'Could not create overlay test window.' }
        try {
            Start-Sleep -Milliseconds 600
            $before = @([FishSmokeNative]::Overlays($process.Id) | Sort-Object Left)
            if ($before.Count -ne 1) { throw 'Only the bobber overlay should be visible without a detected bobber.' }
            [FishSmokeNative]::SetWindowPos($game, 0, 200, 200, 1000, 700, 0x0014) | Out-Null
            for ($attempt = 0; $attempt -lt 30; $attempt++) {
                Start-Sleep -Milliseconds 100
                $after = @([FishSmokeNative]::Overlays($process.Id) | Sort-Object Left)
                if ($after.Count -eq 1 -and $after[0].Left -eq ($before[0].Left + 100) -and $after[0].Top -eq ($before[0].Top + 100)) { break }
            }
            if ($after.Count -ne 1 -or $after[0].Left -ne ($before[0].Left + 100) -or $after[0].Top -ne ($before[0].Top + 100)) {
                throw "Idle overlay mismatch: before $($before[0].Left),$($before[0].Top); after $($after[0].Left),$($after[0].Top); count $($after.Count); test game $game, selected $([FishSmokeNative]::FindGame())."
            }
            $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Minimized)
            for ($attempt = 0; $attempt -lt 30; $attempt++) {
                Start-Sleep -Milliseconds 100
                if (@([FishSmokeNative]::Overlays($process.Id, $true)).Count -eq 0) { break }
            }
            if (@([FishSmokeNative]::Overlays($process.Id, $true)).Count -ne 0) { throw 'Minimizing Fish retained the idle overlays or handles.' }
            $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
            Restore-TestWindow
            Start-Sleep -Milliseconds 400
            if (@([FishSmokeNative]::Overlays($process.Id)).Count -ne 1) { throw 'Restoring Fish did not restore the bobber overlay.' }
            [FishSmokeNative]::ShowWindow($game, 6) | Out-Null
            Start-Sleep -Milliseconds 400
            if (@([FishSmokeNative]::Overlays($process.Id)).Count -ne 0) { throw 'Minimized game retained overlays.' }
            [FishSmokeNative]::ShowWindow($game, 4) | Out-Null
            Start-Sleep -Milliseconds 400
            if (@([FishSmokeNative]::Overlays($process.Id)).Count -ne 1) { throw 'Restoring game did not restore only the bobber overlay.' }
            Write-Output 'PASS splash hidden without bobber; bobber overlay follows window movement and restore'
        } finally { [FishSmokeNative]::DestroyWindow($game) | Out-Null }
    } else { Write-Output 'SKIP synthetic overlay window: WoW is already open.' }
    $liveSettingsTest = !$NoSession -and [FishSmokeNative]::FindGame() -eq [IntPtr]::Zero
    if ($liveSettingsTest) {
        $start.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 400
        if (!$start.Current.IsEnabled -or $start.Current.Name -ne 'Stoppen') { throw 'Session action did not switch to Stoppen.' }
        Write-Output 'PASS session button switches to Stoppen while active'
    } else { Write-Output 'SKIP active UI test: session disabled or WoW is already open.' }
    $tabCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::TabItem)
    $tabs = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tabCondition)
    if ($tabs.Count -ne 0) { throw 'Combined view still contains tabs.' }
    foreach ($removed in @(('WoW schlie' + [string][char]0xdf + 'en'), 'Windows herunterfahren')) {
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $removed)
        if ($window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)) { throw "Removed setting still visible: $removed" }
    }
    foreach ($removedId in @('ThemeOption','AlwaysOnTopOption','MinimizeTrayOption','CloseTrayOption','CheckUpdateButton',
        'BobberAreaWidth','BobberAreaHeight','BobberAreaXOffset','BobberAreaYOffset','BobberTimeoutSeconds',
        'LootDelayMilliseconds','SplashAreaWidth','SplashAreaHeight','SplashAreaXOffset','SplashAreaYOffset','SplashCalibrationMilliseconds',
        'BobberRegionSummary','SaveStatus','VersionLabel','BobberMatchMinimum','BobberColorTolerance','BobberColorToleranceSlider','SplashPixelTolerance','SplashChangePercent')) {
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$removedId)
        if ($window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)) { throw "Removed control remains visible: $removedId" }
    }
    foreach ($removedName in @('Maximale Wurfdauer','Bobber-Timeout','Wartezeit nach Biss','Scan-Intervall','Kalibrierung','Farbschema','Immer im Vordergrund','Normale Wasserbewegung berücksichtigen','Nach Updates suchen')) {
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$removedName)
        if ($window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)) { throw "Removed setting remains visible: $removedName" }
    }
    Write-Output 'PASS combined view has no tabs; hotkey and detection thresholds remain directly accessible'
    function Commit-Settings {
        # Moving focus commits the hotkey without changing the shared view.
        $start.SetFocus()
    }
    Start-Sleep -Milliseconds 300
    $hotkeyInput = Find-Element 'FishingHotkey' -ById
    if (!$hotkeyInput.Current.IsEnabled) { throw 'Fishing hotkey cannot be edited while active.' }
    $hotkeyInput.SetFocus()
    $hotkeyInput.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Ctrl+F8')
    if ($NativeInput) {
        $hotkeyInput.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Shift+F6')
        $captureHotkey = Find-Element 'CaptureFishingHotkey' -ById
        $captureHotkey.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        $captureHotkey.SetFocus()
        [FishSmokeNative]::SetForegroundWindow($mainHandle) | Out-Null
        Start-Sleep -Milliseconds 200
        [FishSmokeNative]::TapKey($mainHandle, 0x77, $true)
        Start-Sleep -Milliseconds 300
        if ($hotkeyInput.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -ne 'Ctrl+F8') { throw 'Hotkey capture did not record Ctrl+F8.' }
        $captureHotkey.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 150
        [FishSmokeNative]::TapKey($mainHandle, 0x1b, $false)
        [FishSmokeNative]::TapKey($mainHandle, 0x51, $false)
        Start-Sleep -Milliseconds 300
        if ($hotkeyInput.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -ne 'Ctrl+F8') { throw 'Escape did not cancel hotkey capture.' }
        Write-Output 'PASS recording Ctrl+F8 and cancelling capture with Escape'
    }
    $bobber = Find-Element 'BobberSensitivity' -ById
    $splash = Find-Element 'SplashSensitivity' -ById
    foreach ($slider in @($bobber,$splash)) {
        $range = $slider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
        if (!$slider.Current.IsEnabled -or $range.Current.Minimum -ne 0 -or $range.Current.Maximum -ne 100 -or $range.Current.Value -ne 50) { throw 'Invalid sensitivity slider defaults or range.' }
    }
    $bobberRange = $bobber.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    $splashRange = $splash.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    $bobberRange.SetValue(70)
    $splashRange.SetValue(80)
    Commit-Settings
    Write-Output 'PASS two independent sensitivity sliders, range 0-100, midpoint 50; no individual detection inputs'
    if ($NativeOverlay) {
        . (Join-Path $PSScriptRoot 'SettingsOverlaySmoke.ps1')
        $overlayFixture = [IntPtr]::Zero
        try {
            if ([FishSmokeNative]::FindGame() -eq [IntPtr]::Zero) {
                $overlayFixture = [FishSmokeNative]::CreateWindowExW(0x08000080, 'STATIC', 'World of Warcraft', 0x10cf0000, 100, 100, 1400, 1000, 0, 0, 0, 0)
                if ($overlayFixture -eq [IntPtr]::Zero) { throw 'Could not create settings overlay fixture.' }
            }
            Start-Sleep -Milliseconds 500
            Test-SettingsOverlay $window $process.Id
        } finally {
            if ($overlayFixture -ne [IntPtr]::Zero) { [FishSmokeNative]::DestroyWindow($overlayFixture) | Out-Null }
        }
    }
    if ($liveSettingsTest) {
        # Exercise repeated live sensitivity changes without stopping the session.
        $bobberRange.SetValue(40)
        Commit-Settings
        Start-Sleep -Milliseconds 300
        $bobberRange.SetValue(70)
        Commit-Settings
        Start-Sleep -Milliseconds 300
        if ($start.Current.Name -ne 'Stoppen') { throw 'Editing settings stopped the active session.' }
        Write-Output 'PASS detection and action settings editable during an active session'
        $start.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        for ($attempt = 0; $attempt -lt 25 -and $start.Current.Name -ne 'Starten'; $attempt++) { Start-Sleep -Milliseconds 200 }
        if (!$start.Current.IsEnabled -or $start.Current.Name -ne 'Starten') { throw 'Session button did not reset after stopping.' }
        Write-Output 'PASS active session stops through the combined button'
    }
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (!$process.WaitForExit(10000)) { throw 'Fish did not close cleanly.' }
    if ($process.ExitCode -ne 0) { throw "Fish exited with code $($process.ExitCode)" }
    Write-Output 'PASS orderly window close'
    $settingsFile = Join-Path $settings 'settings.json'
    if (!(Test-Path -LiteralPath $settingsFile)) { throw 'Settings were not saved on close.' }
    $saved = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
    if ($NativeOverlay -and ($saved.BobberAreaWidth -eq 600 -or $saved.BobberAreaHeight -eq 300 -or $saved.BobberAreaXOffset -eq 0 -or $saved.BobberAreaYOffset -eq 0)) { throw 'Edited bobber geometry was not saved.' }
    if ($saved.FishingHotkey -ne 'Ctrl+F8' -or $saved.SchemaVersion -ne 7) { throw 'Fishing hotkey or settings version was not saved.' }
    if ($saved.BobberSensitivity -ne 70 -or $saved.SplashSensitivity -ne 80) { throw 'Sensitivity sliders were not saved.' }
    foreach ($removedProperty in @('Theme','AlwaysOnTop','MinimizeToTray','CloseToTray','CastTimeoutSeconds','BobberTimeoutSeconds',
        'LootDelayMilliseconds','ScanIntervalMilliseconds','SplashAreaWidth','SplashAreaHeight','SplashAreaXOffset','SplashAreaYOffset',
        'SplashCalibrationMilliseconds','SplashAdaptiveThreshold','WindowWidth','WindowHeight','WindowMaximized','WindowDpi',
        'BobberMatchMinimum','BobberColorTolerance','SplashPixelTolerance','SplashChangePercent')) {
        if ($saved.PSObject.Properties.Name -contains $removedProperty) { throw "Removed setting was still saved: $removedProperty" }
    }
    Write-Output 'PASS automatic save on close'
    $process.Dispose()
    $process = Start-Process @launch
    $window = Wait-FishWindow $process
    $mainHandle = [FishUiWindow]::Find($process.Id)
    Start-Sleep -Milliseconds 1000
    $hotkeyInput = Find-Element 'FishingHotkey' -ById
    if ($hotkeyInput.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -ne 'Ctrl+F8') { throw 'Fishing hotkey was not restored.' }
    Start-Sleep -Milliseconds 300
    foreach ($item in @(@('BobberSensitivity',70),@('SplashSensitivity',80))) {
        $slider = Find-Element $item[0] -ById
        if ($slider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value -ne $item[1]) { throw "Sensitivity was not restored: $($item[0])" }
    }
    if (([FishSmokeNative]::GetWindowLongW($mainHandle, -20) -band 8) -eq 0) { throw 'Restarted window is not topmost.' }
    Write-Output 'PASS automatic reload on restart'
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (!$process.WaitForExit(10000) -or $process.ExitCode -ne 0) { throw 'Restarted Fish did not close cleanly.' }
    Write-Output "Settings: $settings"
}
finally {
    if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }
    $process.Dispose()
    if ($BesideExecutable) {
        foreach ($name in @('settings.json', 'settings.json.bak')) {
            $path = Join-Path $settings $name
            if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
        }
    }
}
