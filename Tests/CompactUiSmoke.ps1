param([Parameter(Mandatory=$true)][string]$Executable, [string]$CaptureDirectory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UiHarness.ps1')
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FishCompactWindow {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr window, uint message, IntPtr wp, IntPtr lp);
}
'@
[FishCompactWindow]::SetThreadDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null
$settings = Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts\ui-tests\compact-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $settings -Force | Out-Null
# Exercise migration from the previous, larger window without starting a fishing session.
@{ SchemaVersion = 4; WindowWidth = 480; WindowHeight = 620; WindowDpi = 96; WindowMaximized = $true } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $settings 'appsettings.json') -Encoding UTF8
$launch = @{ FilePath = (Resolve-Path -LiteralPath $Executable).Path; ArgumentList = @('--settings-directory',$settings); WindowStyle = 'Hidden'; PassThru = $true }
$app = Start-Process @launch
try {
    $root = Wait-FishWindow $app
    $handle = [FishUiWindow]::Find($app.Id)
    [FishCompactWindow]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Milliseconds 700
    function Element([string]$id) {
        $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id))
        if (!$element) { throw "Missing compact control: $id" }
        return $element
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
    if ([FishCompactWindow]::SendMessageW($handle,0x7f,[IntPtr]::Zero,[IntPtr]::Zero) -eq [IntPtr]::Zero -or
        [FishCompactWindow]::SendMessageW($handle,0x7f,[IntPtr]::new(1),[IntPtr]::Zero) -eq [IntPtr]::Zero) { throw 'Custom window/taskbar icons are missing.' }
    $rect = Bounds
    if ([Math]::Abs(($rect.Right-$rect.Left)-480*$scale) -gt 2 -or [Math]::Abs(($rect.Bottom-$rect.Top)-400*$scale) -gt 2) { throw 'Legacy window did not migrate to compact dimensions.' }
    $tabs = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::TabItem))
    if ($tabs.Count -ne 0) { throw 'Tabs remain in the compact view.' }
    $detailControls = @{}
    foreach ($id in @('PreviewSection')) {
        $detailControls[$id] = Element $id
        if ($detailControls[$id].GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Current.ExpandCollapseState -ne 'Collapsed') { throw "Detail area starts expanded: $id" }
    }
    foreach ($id in @('LogSection','LogList','AnalysisSection','PerformancePanel','BrandIcon','StartButton','StopButton','SaveStatus','VersionLabel','BobberRegionSummary','BobberMatchMinimum','BobberColorTolerance','BobberColorToleranceSlider','SplashPixelTolerance','SplashChangePercent')) {
        if ($root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id))) { throw "Removed control remains: $id" }
    }
    $button = Element 'SessionButton'
    $progress = Element 'StepTimeProgress'
    if ($button.Current.Name -ne 'Starten' -or $button.Current.BoundingRectangle.Bottom -gt $progress.Current.BoundingRectangle.Top) { throw 'Session action is not above progress in the status card.' }
    $settingsControls = @{}
    foreach ($id in @('FishingHotkey','BobberSensitivity','SplashSensitivity')) { $settingsControls[$id] = Element $id }
    $sliders = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Slider))
    $edits = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit))
    if ($sliders.Count -ne 2 -or $edits.Count -ne 1) { throw 'Expected exactly two sensitivity sliders and only the hotkey text input.' }
    $viewport = (Element 'MainScroll').Current.BoundingRectangle
    foreach ($id in @('BobberSensitivity','SplashSensitivity')) {
        if ($settingsControls[$id].Current.BoundingRectangle.Width -lt $viewport.Width - 36*$scale) { throw "Sensitivity slider does not span the available width: $id" }
    }
    foreach ($label in @('Gering','Hoch')) {
        $labels = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$label))
        if ($labels.Count -ne 2) { throw "Missing sensitivity endpoint labels: $label" }
    }
    foreach ($item in @(@('BobberSensitivity','Schwimmer-Toleranz'),@('SplashSensitivity','Spritzer-Toleranz'))) {
        if ($settingsControls[$item[0]].Current.Name -ne $item[1]) { throw "Outdated tolerance label: $($item[0])" }
    }
    foreach ($label in @('Erkennungstoleranz','Überwachung')) {
        if (!$root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$label))) { throw "Missing grouped settings label: $label" }
    }
    if ((Element 'CaptureFishingHotkey').Current.Name -ne 'Angeln-Tastenkombination setzen') { throw 'Hotkey button was not renamed to Setzen.' }
    if ($root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'50'))) { throw 'Numeric sensitivity label remains.' }
    Capture 'compact-default.png'
    Write-Output 'PASS Schwimmer/Spritzer tolerance sliders with Gering/Hoch endpoints; goldfish window icons present'
    [FishCompactWindow]::SetWindowPos($handle,[IntPtr]::Zero,0,0,[int](400*$scale),[int](360*$scale),0x0016) | Out-Null
    Start-Sleep -Milliseconds 400
    $scroll = Element 'MainScroll'
    $editCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit)
    foreach ($id in @('FishingHotkey','BobberSensitivity','SplashSensitivity')) {
        $control = $settingsControls[$id]
        $edit = $control.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$editCondition)
        $focus = if ($edit) { $edit } else { $control }
        $focus.SetFocus()
        Start-Sleep -Milliseconds 120
        $bounds = $focus.Current.BoundingRectangle; $viewport = $scroll.Current.BoundingRectangle
        if ($focus.Current.IsOffscreen -or $bounds.Width -le 0 -or $bounds.Left -lt $viewport.Left-2 -or $bounds.Right -gt $viewport.Right+2) { throw "Control is clipped at compact width: $id" }
    }
    Capture 'compact-small.png'
    Write-Output 'PASS all settings remain focusable and horizontally visible at 400x360 logical pixels'
    (Element 'SessionButton').SetFocus()
    foreach ($id in @('PreviewSection')) {
        $detailControls[$id].GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    }
    Start-Sleep -Milliseconds 700
    $scrollPattern = $scroll.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    if (!$scrollPattern.Current.VerticallyScrollable) { throw 'Expanded detail areas cannot be scrolled.' }
    for ($step=0; $step -lt 8 -and $scrollPattern.Current.VerticalScrollPercent -lt 99; $step++) { $scrollPattern.Scroll([System.Windows.Automation.ScrollAmount]::NoAmount,[System.Windows.Automation.ScrollAmount]::LargeIncrement); Start-Sleep -Milliseconds 100 }
    Start-Sleep -Milliseconds 400
    if ($scrollPattern.Current.VerticalScrollPercent -lt 95) {
        Capture 'compact-scroll-debug.png'
        Write-Output ("Scroll: percent={0}, view={1}; bounds={2}" -f $scrollPattern.Current.VerticalScrollPercent,$scrollPattern.Current.VerticalViewSize,$scroll.Current.BoundingRectangle)
        foreach ($id in @('PreviewSection')) { Write-Output ("{0}: {1}, bounds={2}" -f $id,$detailControls[$id].GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Current.ExpandCollapseState,$detailControls[$id].Current.BoundingRectangle) }
        throw 'Shared view jumped away from the selected details.'
    }
    Capture 'compact-details.png'
    Write-Output 'PASS live preview remains reachable in the shared scroll view at compact size'
    [FishCompactWindow]::SetWindowPos($handle,[IntPtr]::Zero,0,0,[int](480*$scale),[int](930*$scale),0x0016) | Out-Null
    Start-Sleep -Milliseconds 500
    function PreviewHeading([string]$name) {
        $matches = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name))
        return ($matches | Where-Object { $_.Current.BoundingRectangle.Width -gt 0 } |
            Sort-Object { $_.Current.BoundingRectangle.Top } -Descending | Select-Object -First 1).Current.BoundingRectangle
    }
    # Empty Image controls have no UIA bounds until a frame arrives; inspect the visible preview headings.
    $live = PreviewHeading 'Live'
    $bobber = PreviewHeading 'Schwimmer'
    $splash = PreviewHeading 'Spritzer'
    $monitor = $detailControls['PreviewSection'].Current.BoundingRectangle
    Capture 'monitoring-layout.png'
    if ($live.Top - $bobber.Bottom -lt 110*$scale -or [Math]::Abs($live.Left-$bobber.Left) -gt 2 -or
        [Math]::Abs($bobber.Top-$splash.Top) -gt 2 -or $bobber.Left -ge $splash.Left -or
        $monitor.Bottom - $live.Bottom -le 160*$scale) { throw 'Monitoring preview layout is not larger and correctly grouped.' }
    $bars = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ScrollBar))
    if (@($bars | Where-Object { !$_.Current.IsOffscreen -and $_.Current.BoundingRectangle.Width -gt 0 }).Count -ne 0) { throw 'A scrollbar is visible.' }
    foreach ($label in @('Noch kein Bild','Keine aktive Aufnahme')) {
        if ($root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$label))) { throw 'Old preview captions remain.' }
    }
    Capture 'monitoring-layout.png'
    Write-Output 'PASS Schwimmer/Spritzer above the large Live image; no visible scrollbars or captions'
    $previousHeight = $monitor.Height
    [FishCompactWindow]::SetWindowPos($handle,[IntPtr]::Zero,0,0,[int](480*$scale),[int](830*$scale),0x0016) | Out-Null
    Start-Sleep -Milliseconds 500
    $smallerHeight = $detailControls['PreviewSection'].Current.BoundingRectangle.Height
    if ($smallerHeight -ge $previousHeight-50*$scale) { throw 'Image areas did not adapt to a vertical window resize.' }
    Write-Output 'PASS preview areas resize with window height as well as width'
    $root.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (!$app.WaitForExit(10000) -or $app.ExitCode -ne 0) { throw 'Compact Fish did not close cleanly.' }
    $saved = Get-Content (Join-Path $settings 'appsettings.json') -Raw | ConvertFrom-Json
    if ($saved.SchemaVersion -ne 6) { throw 'Compact layout migration was not persisted.' }
    Write-Output "Settings: $settings"
} finally {
    $app.Refresh()
    if (!$app.HasExited) { $app.Kill(); $app.WaitForExit() }
    $app.Dispose()
}
