param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UiHarness.ps1')
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FishSessionFixture {
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowW(string cls, string title);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
    public static void CheckEmptyRegion() {
        var game = FindWindowW(null, "World of Warcraft");
        Rect rect;
        if (game != IntPtr.Zero && (!GetClientRect(game, out rect) || rect.Right > 14000 || rect.Bottom > 8000))
            throw new InvalidOperationException("Cannot guarantee an empty search region; refusing session test.");
    }
}
'@
# An entirely offscreen search area makes FishingSession return before any input or capture.
# Check the current client's size first; never alter the test region while the session is active.
[FishSessionFixture]::CheckEmptyRegion()
$settings = Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts\ui-tests\session-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $settings -Force | Out-Null
@{ SchemaVersion = 6; BobberAreaWidth = 100; BobberAreaHeight = 100; BobberAreaXOffset = 7680; BobberAreaYOffset = 4320 } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $settings 'appsettings.json') -Encoding UTF8
$app = Start-Process -FilePath (Resolve-Path -LiteralPath $Executable).Path -ArgumentList "--settings-directory `"$settings`"" -WindowStyle Hidden -PassThru
try {
    $root = Wait-FishWindow $app
    function ById([string]$id) {
        for ($attempt = 0; $attempt -lt 50; $attempt++) {
            $found = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
                [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id))
            if ($found) { return $found }
            Start-Sleep -Milliseconds 100
        }
        throw "Control missing: $id"
    }
    function Wait-State([bool]$running) {
        for ($attempt = 0; $attempt -lt 40; $attempt++) {
            $expected = if ($running) { 'Stoppen' } else { 'Starten' }
            if ($button.Current.Name -eq $expected -and $button.Current.IsEnabled) { return }
            Start-Sleep -Milliseconds 100
        }
        throw "Unexpected session button for running=$running : $($button.Current.Name)/$($button.Current.IsEnabled)"
    }
    function Invoke($button) { $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
    $button = ById 'SessionButton'
    Wait-State $false
    Invoke $button
    Wait-State $true
    foreach ($id in @('BobberSensitivity','SplashSensitivity')) {
        (ById $id).GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(70)
    }
    Wait-State $true
    Write-Output 'PASS both sensitivity sliders remain editable during an active session'
    foreach ($removed in @('Pause', 'Fortsetzen', 'PAUSIERT')) {
        if ($root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $removed))) {
            throw "Removed pause control still visible: $removed"
        }
    }
    Invoke $button
    Wait-State $false
    Invoke $button
    Wait-State $true
    Invoke $button
    Wait-State $false
    Write-Output 'PASS single button switches Starten/Stoppen; stopping resets the session; restarting works'
    Invoke $button
    Wait-State $true
    $root.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (!$app.WaitForExit(10000) -or $app.ExitCode -ne 0) { throw 'Fish did not close cleanly.' }
    Write-Output 'PASS closing while active stops the session and exits cleanly'
} finally {
    if (!$app.HasExited) { $app.Kill(); $app.WaitForExit() }
    $app.Dispose()
}
