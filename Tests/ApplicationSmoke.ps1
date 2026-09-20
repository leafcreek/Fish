param([switch]$SkipTray)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class FishApplicationSmoke {
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr w, int cmd);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr w);
 [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr w, uint m, IntPtr wp, IntPtr lp);
}
"@
$workspace = Split-Path $PSScriptRoot -Parent
$settings = Join-Path $workspace ('artifacts/application-smoke-' + [guid]::NewGuid().ToString('N'))
$app = Start-Process -FilePath (Join-Path $workspace 'bin/Debug/net10.0-windows10.0.26100.0/Fish.exe') -ArgumentList "--settings-directory `"$settings`"" -WindowStyle Hidden -PassThru
try {
 Start-Sleep -Seconds 3
 $app.Refresh()
 if ($app.HasExited) { throw "Application exited: $($app.ExitCode)" }
 $window = $app.MainWindowHandle
 $root = if ($window -ne 0) { [System.Windows.Automation.AutomationElement]::FromHandle($window) } else { $null }
 function Find-Element([string]$name) { return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name)) }
 $skipGuide = Find-Element 'Überspringen'
 if ($skipGuide) { $skipGuide.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 500 }
 $deadline = [DateTime]::UtcNow.AddSeconds(20)
 do {
   $app.Refresh()
   if ($app.HasExited) { throw "Application exited before UI became ready: $($app.ExitCode)" }
   $window = $app.MainWindowHandle
   if ($window -ne 0) { $root = if ($window -ne 0) { [System.Windows.Automation.AutomationElement]::FromHandle($window) } else { $null }; $navigation = Find-Element 'Anwendung' }
   if (!$navigation) { Start-Sleep -Milliseconds 250 }
 } while (!$navigation -and [DateTime]::UtcNow -lt $deadline)
 if (!$navigation) { throw 'Application page was not available within 20 seconds' }
 $navigation.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 Start-Sleep -Milliseconds 300
 (Find-Element 'Sitzungsende').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
 Start-Sleep -Seconds 1
 if (!(Get-Content (Join-Path $settings 'appsettings.json') -Raw | ConvertFrom-Json).NotifySessionEnd) { throw 'Autosave failed' }
 Write-Output 'PASS Autosave before exit'
 if (!$SkipTray) {
 [FishApplicationSmoke]::ShowWindow($window,6) | Out-Null
 Start-Sleep -Seconds 2
 if ([FishApplicationSmoke]::IsWindowVisible($window)) {
   $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name } | Where-Object { $_ -match 'Tray|fehlgeschlagen' } | Write-Output
   throw 'Minimize to tray failed'
 }
 Write-Output 'PASS Minimize hides window in tray'
 [FishApplicationSmoke]::PostMessageW($window,0x8001,0,0x10400) | Out-Null
 Start-Sleep -Seconds 1
 if (![FishApplicationSmoke]::IsWindowVisible($window)) { throw 'Tray restore failed' }
 Write-Output 'PASS Tray activation restores window'
 } else { Write-Output 'SKIP Tray: environment without accessible Windows notification area' }
 (Find-Element 'Bericht speichern').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Seconds 1
 if (!(Test-Path (Join-Path $settings 'diagnostics/Fish-*.txt'))) { throw 'Diagnostics export failed' }
 Write-Output 'PASS Diagnostics export'
 $updateButton = Find-Element 'Nach Updates suchen'
 $updateButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Milliseconds 300
 $skipGuide = Find-Element 'Überspringen'
 if ($skipGuide) { $skipGuide.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 500 }
 $deadline = [DateTime]::UtcNow.AddSeconds(20)
 while (!$updateButton.Current.IsEnabled -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 250 }
 if (!$updateButton.Current.IsEnabled) { throw 'Update check did not finish' }
 $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name } | Where-Object { $_ -match 'Globaler Stop aktiv|Veröffentlichung|Updateprüfung fehlgeschlagen|Neue Version' } | Select-Object -Unique
 Write-Output "Test settings: $settings"
} finally {
 $app.Refresh()
 if (!$app.HasExited) {
   [FishApplicationSmoke]::ShowWindow($window,9) | Out-Null; $app.CloseMainWindow() | Out-Null
   if (!$app.WaitForExit(3000)) { $app.Kill() } # Only this test-owned process; no session is ever started.
 }
}
