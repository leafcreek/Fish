param([Parameter(Mandatory=$true)][string]$Executable, [string]$CaptureDirectory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UiHarness.ps1')
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class FishCompactWindow {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern int GetWindowLongW(IntPtr window, int index);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr window, uint message, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    private static void MouseAt(int x, int y, uint button) {
        int left=GetSystemMetrics(76), top=GetSystemMetrics(77), width=GetSystemMetrics(78), height=GetSystemMetrics(79);
        uint nx=(uint)Math.Max(0,Math.Min(65535,(x-left+.5)*65536.0/width));
        uint ny=(uint)Math.Max(0,Math.Min(65535,(y-top+.5)*65536.0/height));
        mouse_event(0x8000|0x4000|1|button,nx,ny,0,UIntPtr.Zero);
    }
    public static void ClickOwn(uint process, int x, int y) {
        if ((GetAsyncKeyState(1) & 0x8000) != 0) throw new InvalidOperationException("Release the mouse button before the test.");
        SetCursorPos(x,y);
        uint owner; GetWindowThreadProcessId(WindowFromPoint(new Point { X=x,Y=y }), out owner);
        if (owner != process) throw new InvalidOperationException("Refusing to click outside the test's own window.");
        MouseAt(x,y,2); System.Threading.Thread.Sleep(25); MouseAt(x,y,4);
    }
    public static void DragOwn(uint process, int x, int y, int dx, int dy) {
        if ((GetAsyncKeyState(1) & 0x8000) != 0) throw new InvalidOperationException("Release the mouse button before the test.");
        SetCursorPos(x,y);
        uint owner; GetWindowThreadProcessId(WindowFromPoint(new Point { X=x,Y=y }), out owner);
        if (owner != process) throw new InvalidOperationException("Refusing to drag outside the test's own window.");
        MouseAt(x,y,2);
        try { System.Threading.Thread.Sleep(120); MouseAt(x+dx,y+dy,0); System.Threading.Thread.Sleep(120); }
        finally { MouseAt(x+dx,y+dy,4); }
    }
}
"@
[FishCompactWindow]::SetThreadDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null
$settings = Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts\ui-tests\compact-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $settings -Force | Out-Null
# Old saved dimensions/maximization must not override the fixed, collapsed startup layout.
@{ SchemaVersion = 7; WindowWidth = 820; WindowHeight = 900; WindowDpi = 96; WindowMaximized = $true } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $settings 'settings.json') -Encoding UTF8
$launch = @{ FilePath = (Resolve-Path -LiteralPath $Executable).Path; ArgumentList = @('--settings-directory',$settings); WindowStyle = 'Hidden'; PassThru = $true }
$app = Start-Process @launch
$originalCursor = [FishCompactWindow+Point]::new()
[FishCompactWindow]::GetCursorPos([ref]$originalCursor) | Out-Null
try {
    $root = Wait-FishWindow $app
    $handle = [FishUiWindow]::Find($app.Id)
    [FishCompactWindow]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Milliseconds 500
    function Element([string]$id) {
        $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id))
        if (!$element) { throw "Missing compact control: $id" }
        return $element
    }
    function Named([string]$name) {
        return $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name))
    }
    function Bounds {
        $rect = [FishCompactWindow+Rect]::new()
        if (![FishCompactWindow]::GetWindowRect($handle,[ref]$rect)) { throw 'No own window bounds.' }
        return $rect
    }
    function Capture([string]$name) {
        if (!$CaptureDirectory) { return }
        New-Item -ItemType Directory -Path $CaptureDirectory -Force | Out-Null
        $rect = Bounds
        $bitmap = [Drawing.Bitmap]::new($rect.Right-$rect.Left,$rect.Bottom-$rect.Top)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size)
            $bitmap.Save((Join-Path $CaptureDirectory $name),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
    $scale = [FishCompactWindow]::GetDpiForWindow($handle) / 96.0
    $compact = Bounds
    if ([Math]::Abs(($compact.Right-$compact.Left)-480*$scale) -gt 2 -or [Math]::Abs(($compact.Bottom-$compact.Top)-480*$scale) -gt 2) {
        throw 'Saved dimensions overrode the fixed 480x480 startup layout.'
    }
    $window = $root.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    if ($window.Current.CanMaximize -or ([FishCompactWindow]::GetWindowLongW($handle,-16) -band 0x00040000) -ne 0) {
        throw 'Manual resize or maximize remains enabled.'
    }
    $monitor = Element 'PreviewSection'
    $expander = $monitor.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    if ($expander.Current.ExpandCollapseState -ne 'Collapsed') { throw 'Monitoring must start collapsed.' }
    foreach ($icon in @(0,1)) {
        if ([FishCompactWindow]::SendMessageW($handle,0x7f,[IntPtr]::new($icon),[IntPtr]::Zero) -eq [IntPtr]::Zero) { throw 'Goldfish icon is missing.' }
    }
    Write-Output 'PASS fixed 480x480 startup; resize and maximize disabled; monitoring collapsed; goldfish icons present'
    $titleBar = Element 'AppTitleBar'
    $version = Element 'VersionText'
    $expectedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($launch.FilePath).ProductVersion.Split('+')[0]
    if ($version.Current.Name -ne ('Fish Version ' + $expectedVersion) -or $version.Current.IsOffscreen -or
        $version.Current.BoundingRectangle.Top -le $monitor.Current.BoundingRectangle.Bottom) { throw 'Footer does not show the actual build version below the content.' }
    if ($titleBar.Current.IsOffscreen -or $titleBar.Current.Name -ne 'Fish-Titelleiste') { throw 'Custom title bar is missing.' }
    $beforeDrag = Bounds; $dragArea = $titleBar.Current.BoundingRectangle
    $dragX = [int]($dragArea.Left+120*$scale); $dragY = [int]($dragArea.Top+$dragArea.Height/2)
    $dragHit = [FishCompactWindow]::SendMessageW($handle,0x84,[IntPtr]::Zero,[IntPtr]::new(($dragY -shl 16) -bor ($dragX -band 65535)))
    [FishCompactWindow]::DragOwn($app.Id,[int]($dragArea.Left+120*$scale),[int]($dragArea.Top+$dragArea.Height/2),32,24)
    Start-Sleep -Milliseconds 250
    $afterDrag = Bounds
    # Windows may apply drag thresholds or snap offsets; verify actual movement and unchanged size.
    if ($dragHit -ne [IntPtr]::new(2) -or [Math]::Abs($afterDrag.Left-$beforeDrag.Left) -lt 8 -or
        [Math]::Abs($afterDrag.Top-$beforeDrag.Top) -lt 8 -or
        ($afterDrag.Right-$afterDrag.Left) -ne ($beforeDrag.Right-$beforeDrag.Left) -or
        ($afterDrag.Bottom-$afterDrag.Top) -ne ($beforeDrag.Bottom-$beforeDrag.Top)) {
        throw "Dragging the custom title bar does not move the window: hit=$dragHit, before=$($beforeDrag.Left),$($beforeDrag.Top), after=$($afterDrag.Left),$($afterDrag.Top), title=$dragArea"
    }
    Write-Output 'PASS custom title bar drags natively; footer shows the actual assembly version'
    $button = Element 'SessionButton'
    $progress = Element 'StepTimeProgress'
    if ($button.Current.Name -ne 'Starten' -or $button.Current.BoundingRectangle.Bottom -gt $progress.Current.BoundingRectangle.Top -or
        $progress.Current.BoundingRectangle.Height -lt 8*$scale-1) { throw 'Status card action/progress layout is incorrect.' }
    $sliders = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Slider))
    $edits = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit))
    if ($sliders.Count -ne 2 -or $edits.Count -ne 0) { throw 'Expected two sliders and display-only hotkeys.' }
    $viewport = (Element 'MainScroll').Current.BoundingRectangle
    foreach ($id in @('BobberSensitivity','SplashSensitivity')) {
        if ((Element $id).Current.BoundingRectangle.Width -lt $viewport.Width-36*$scale) { throw "Slider is not full width: $id" }
    }
    foreach ($label in @('Gering','Hoch')) {
        $labels = Named $label
        if ($labels.Count -ne 1) { throw "Expected one shared endpoint label: $label" }
        if ($labels[0].Current.BoundingRectangle.Top -lt (Element 'SplashSensitivity').Current.BoundingRectangle.Bottom-1) {
            throw "Endpoint label is not below the Biss slider: $label"
        }
    }
    $low = (Named 'Gering')[0].Current.BoundingRectangle; $high = (Named 'Hoch')[0].Current.BoundingRectangle
    $sliderBounds = (Element 'SplashSensitivity').Current.BoundingRectangle
    if ([Math]::Abs($low.Left-$sliderBounds.Left) -gt 2 -or [Math]::Abs($high.Right-$sliderBounds.Right) -gt 2 -or
        (Named 'Gering ↔ Hoch').Count -ne 0) { throw 'Shared tolerance endpoints are not aligned at the slider edges.' }
    foreach ($id in @('FooterState','FooterStatusDot')) {
        if ($root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id))) {
            throw "Removed footer status remains: $id"
        }
    }
    $fishingHotkey = Element 'FishingHotkey'
    if ($fishingHotkey.Current.BoundingRectangle.Width -gt 100*$scale) { throw 'Hotkey input is still too wide.' }
    if ($monitor.Current.IsOffscreen -or $monitor.Current.BoundingRectangle.Bottom -gt $viewport.Bottom+1) { throw 'Monitoring header is clipped.' }
    $baitCheckbox = Element 'BaitEnabled'
    $baitLabel = Element 'BaitSettingLabel'
    $durationButton = Element 'BaitFiveMinutes'
    $macroButton = Element 'CopyBaitMacro'
    $timerButton = Element 'BaitTimerToggle'
    $baitHotkey = Element 'BaitHotkey'
    $baitToggle = $baitCheckbox.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $durationAction = $durationButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $baitProgress = Element 'BaitTimeProgress'
    $baitTime = Element 'BaitTimeText'
    $baitRange = $baitProgress.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    foreach ($hotkey in @($fishingHotkey, $baitHotkey)) {
        if ($hotkey.Current.ControlType -ne [System.Windows.Automation.ControlType]::Text -or
            $hotkey.Current.IsKeyboardFocusable) {
            throw 'Both hotkeys should be static text with no keyboard focus.'
        }
    }
    if ($fishingHotkey.Current.Name -ne '1' -or $baitHotkey.Current.Name -ne '2') {
        throw 'The displays should show the default hotkeys.'
    }
    if ($baitLabel.Current.ControlType -ne [System.Windows.Automation.ControlType]::Text) {
        throw 'Expected a standalone bait label.'
    }
    $labelBounds = $baitLabel.Current.BoundingRectangle
    $checkboxBounds = $baitCheckbox.Current.BoundingRectangle
    if ($checkboxBounds.Right -gt $labelBounds.Left -or
        [Math]::Abs(($labelBounds.Top+$labelBounds.Bottom)-($checkboxBounds.Top+$checkboxBounds.Bottom)) -gt 4*$scale) {
        throw 'Bait label should be centered next to its checkbox.'
    }
    if ($baitToggle.Current.ToggleState -ne 'Off' -or $durationButton.Current.Name -ne '10 min' -or
        $fishingHotkey.Current.BoundingRectangle.Bottom -gt $baitHotkey.Current.BoundingRectangle.Top -or
        $checkboxBounds.Left -gt $viewport.Left+12*$scale -or
        $macroButton.Current.Name -ne 'Makro' -or $macroButton.Current.IsOffscreen -or $timerButton.Current.IsEnabled) {
        throw 'Bait defaults, settings row, or macro button are incorrect.'
    }
    $macroBounds = $macroButton.Current.BoundingRectangle
    $timerBounds = $timerButton.Current.BoundingRectangle
    $durationBounds = $durationButton.Current.BoundingRectangle
    $macroGap = $macroBounds.Left - $labelBounds.Right
    $durationGap = $baitHotkey.Current.BoundingRectangle.Left - $durationBounds.Right
    $timerGap = $timerBounds.Left - $macroBounds.Right
    if ($macroGap -lt 0 -or $macroGap -gt 6*$scale -or $timerGap -lt 0 -or $timerGap -gt 6*$scale -or
        $timerBounds.Right -gt $durationBounds.Left -or
        $durationGap -lt 0 -or $durationGap -gt 20*$scale) {
        throw 'Makro and the timer button should sit next to the bait label, with duration next to the hotkey.'
    }
    foreach ($settingsButton in @($macroButton, (Element 'CaptureFishingHotkey'), (Element 'CaptureBaitHotkey'))) {
        $bounds = $settingsButton.Current.BoundingRectangle
        if ([Math]::Abs($durationBounds.Width-$bounds.Width) -gt 2 -or
            [Math]::Abs($durationBounds.Height-$bounds.Height) -gt 2) {
            throw 'The duration button should match the Makro and Setzen buttons in size.'
        }
    }
    function Assert-BaitDisplay([string]$expected) {
        if ($baitProgress.Current.IsOffscreen -or
            $progress.Current.BoundingRectangle.Bottom -gt $baitProgress.Current.BoundingRectangle.Top -or
            $baitRange.Current.Value -ne 0 -or $baitTime.Current.Name -ne $expected) {
            throw "Expected empty bait progress below the step progress and status $expected."
        }
    }
    Assert-BaitDisplay 'Deaktiviert'
    foreach ($expectedDuration in @('5 min', '10 min', '5 min')) {
        $durationAction.Invoke()
        Start-Sleep -Milliseconds 150
        if ($durationButton.Current.Name -ne $expectedDuration) {
            throw "Clicking the duration button should switch it to $expectedDuration."
        }
    }
    $baitToggle.Toggle()
    Start-Sleep -Milliseconds 300
    Assert-BaitDisplay 'Inaktiv'
    $timerAction = $timerButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    if (!$timerButton.Current.IsEnabled -or $timerButton.Current.Name -ne 'Ködertimer starten') {
        throw 'Expected the timer button to offer Start.'
    }
    $timerAction.Invoke()
    Start-Sleep -Milliseconds 300
    if ($timerButton.Current.Name -ne 'Ködertimer stoppen' -or $baitRange.Current.Value -le 0 -or
        $baitTime.Current.Name -notmatch '^[0-9]+:[0-5][0-9]$') {
        throw 'Starting the timer should offer Stop and show a live countdown.'
    }
    $timerAction.Invoke()
    Start-Sleep -Milliseconds 300
    Assert-BaitDisplay 'Inaktiv'
    if ($timerButton.Current.Name -ne 'Ködertimer starten') { throw 'Stopping the timer should offer Start.' }
    $baitToggle.Toggle()
    Start-Sleep -Milliseconds 300
    Assert-BaitDisplay 'Deaktiviert'
    Write-Output 'PASS bait settings row, timer start and stop, duration button, and persistent progress visibility'
    Capture 'compact-default.png'
    Write-Output 'PASS shared Gering/Hoch endpoints below both sliders; footer has version only; all compact controls visible'
    # Even an external programmatic size change must return to the selected fixed layout.
    [FishCompactWindow]::SetWindowPos($handle,[IntPtr]::Zero,0,0,[int](600*$scale),[int](600*$scale),0x0016) | Out-Null
    Start-Sleep -Milliseconds 400
    $rect = Bounds
    if (($rect.Right-$rect.Left) -ne ($compact.Right-$compact.Left) -or ($rect.Bottom-$rect.Top) -ne ($compact.Bottom-$compact.Top)) { throw 'Fixed window size was not restored.' }
    $expander.Expand()
    Start-Sleep -Milliseconds 700
    $expanded = Bounds
    if (($expanded.Right-$expanded.Left) -ne ($compact.Right-$compact.Left) -or ($expanded.Bottom-$expanded.Top) -le ($compact.Bottom-$compact.Top)+150*$scale) {
        throw 'Opening monitoring must increase only the height.'
    }
    function PreviewHeading([string]$name) {
        return ((Named $name) | Where-Object { !$_.Current.IsOffscreen -and $_.Current.BoundingRectangle.Width -gt 0 } |
            Sort-Object { $_.Current.BoundingRectangle.Top } -Descending | Select-Object -First 1).Current.BoundingRectangle
    }
    $bobber = PreviewHeading 'Schwimmer'; $splash = PreviewHeading 'Biss'; $live = PreviewHeading 'Live'
    $viewport = (Element 'MainScroll').Current.BoundingRectangle
    if ([Math]::Abs($bobber.Top-$splash.Top) -gt 2 -or $bobber.Left -ge $splash.Left -or
        $live.Top -le $bobber.Bottom+60*$scale -or [Math]::Abs($live.Left-$bobber.Left) -gt 2 -or
        $monitor.Current.BoundingRectangle.Bottom -gt $viewport.Bottom+2) { throw 'Expanded previews are clipped or incorrectly arranged.' }
    $scroll = (Element 'MainScroll').GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    Capture 'monitoring-layout.png'
    if ($scroll.Current.VerticallyScrollable) { throw "Expanded monitoring should fit without scrolling: view=$($scroll.Current.VerticalViewSize), viewport=$viewport, monitor=$($monitor.Current.BoundingRectangle)." }
    Write-Output 'PASS monitoring expands height only; all three previews fit without scrolling'
    foreach ($cycle in 1..10) {
        $expander.Collapse(); Start-Sleep -Milliseconds 350
        $rect = Bounds
        if (($rect.Right-$rect.Left) -ne ($compact.Right-$compact.Left) -or ($rect.Bottom-$rect.Top) -ne ($compact.Bottom-$compact.Top)) { throw 'Collapsing monitoring changed width or failed to restore height.' }
        $expander.Expand(); Start-Sleep -Milliseconds 350
        $rect = Bounds
        if (($rect.Right-$rect.Left) -ne ($expanded.Right-$expanded.Left) -or ($rect.Bottom-$rect.Top) -ne ($expanded.Bottom-$expanded.Top)) { throw 'Repeated toggles changed the fixed dimensions.' }
    }
    Write-Output 'PASS repeated monitoring toggles restore exact heights without width drift'
    function Click-Monitor {
        $header = $monitor.Current.BoundingRectangle
        $x = [int]($header.Left+$header.Width/2); $y = [int]($header.Top+16*$scale)
        $hit = [FishCompactWindow]::SendMessageW($handle,0x84,[IntPtr]::Zero,[IntPtr]::new(($y -shl 16) -bor ($x -band 65535)))
        if ($hit -ne [IntPtr]::new(1)) { throw "Monitoring header is incorrectly part of the native caption: hit=$hit" }
        [FishCompactWindow]::ClickOwn($app.Id,[int]($header.Left+$header.Width/2),[int]($header.Top+16*$scale))
    }
    function Wait-Monitor([string]$state, [int]$height) {
        for ($attempt=0; $attempt -lt 20; $attempt++) {
            $rect = Bounds
            if ($expander.Current.ExpandCollapseState -eq $state -and ($rect.Bottom-$rect.Top) -eq $height) { return }
            Start-Sleep -Milliseconds 50
        }
    }
    foreach ($cycle in 1..12) {
        Click-Monitor
        Wait-Monitor 'Collapsed' ($compact.Bottom-$compact.Top)
        $rect = Bounds
        if ($expander.Current.ExpandCollapseState -ne 'Collapsed' -or ($rect.Right-$rect.Left) -ne ($compact.Right-$compact.Left) -or
            ($rect.Bottom-$rect.Top) -ne ($compact.Bottom-$compact.Top)) {
            Capture 'native-collapse-debug.png'
            throw "Native collapse changed dimensions in cycle ${cycle}: state=$($expander.Current.ExpandCollapseState), actual=$($rect.Right-$rect.Left)x$($rect.Bottom-$rect.Top), expected=$($compact.Right-$compact.Left)x$($compact.Bottom-$compact.Top)"
        }
        Click-Monitor
        Wait-Monitor 'Expanded' ($expanded.Bottom-$expanded.Top)
        $rect = Bounds
        if ($expander.Current.ExpandCollapseState -ne 'Expanded' -or ($rect.Right-$rect.Left) -ne ($expanded.Right-$expanded.Left) -or
            ($rect.Bottom-$rect.Top) -ne ($expanded.Bottom-$expanded.Top)) {
            Capture 'native-expansion-debug.png'
            throw "Native expansion changed dimensions in cycle ${cycle}: state=$($expander.Current.ExpandCollapseState), actual=$($rect.Right-$rect.Left)x$($rect.Bottom-$rect.Top), expected=$($expanded.Right-$expanded.Left)x$($expanded.Bottom-$expanded.Top)"
        }
    }
    foreach ($cycle in 1..8) { Click-Monitor; Start-Sleep -Milliseconds 40; Click-Monitor; Start-Sleep -Milliseconds 40 }
    Start-Sleep -Milliseconds 700
    $rect = Bounds
    if (($rect.Right-$rect.Left) -ne ($expanded.Right-$expanded.Left) -or ($rect.Bottom-$rect.Top) -ne ($expanded.Bottom-$expanded.Top)) {
        throw 'Rapid native toggles grew the window.'
    }
    if ($scroll.Current.VerticallyScrollable) { throw 'Repeated native toggles grew the preview content.' }
    Write-Output 'PASS 40 native mouse toggles, including rapid reversals, preserve window and preview heights'
    $window.Close()
    if (!$app.WaitForExit(10000) -or $app.ExitCode -ne 0) { throw 'Fish did not close cleanly.' }
    $saved = Get-Content -LiteralPath (Join-Path $settings 'settings.json') -Raw | ConvertFrom-Json
    if ($saved.BaitFiveMinutes -ne $true) { throw 'Bait duration was not saved.' }
    foreach ($removed in @('WindowWidth','WindowHeight','WindowMaximized','WindowDpi')) {
        if ($saved.PSObject.Properties.Name -contains $removed) { throw "Fixed window setting was persisted: $removed" }
    }
    $app.Dispose()
    $app = Start-Process @launch
    $root = Wait-FishWindow $app; $handle = [FishUiWindow]::Find($app.Id)
    Start-Sleep -Milliseconds 500
    $rect = Bounds
    $expander = (Element 'PreviewSection').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    if ($expander.Current.ExpandCollapseState -ne 'Collapsed' -or ($rect.Bottom-$rect.Top) -ne ($compact.Bottom-$compact.Top)) { throw 'Monitoring did not restart collapsed.' }
    if ((Element 'BaitFiveMinutes').Current.Name -ne '5 min') { throw 'Bait duration was not restored.' }
    $root.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (!$app.WaitForExit(10000) -or $app.ExitCode -ne 0) { throw 'Fish restart did not close cleanly.' }
    Write-Output 'PASS restart is compact and collapsed even after closing with monitoring open'
    Write-Output "Settings: $settings"
} finally {
    [FishCompactWindow]::SetCursorPos($originalCursor.X,$originalCursor.Y) | Out-Null
    $app.Refresh()
    if (!$app.HasExited) { $app.Kill(); $app.WaitForExit() }
    $app.Dispose()
}
