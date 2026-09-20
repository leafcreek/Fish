$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class FishExperienceSmoke {
 [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowW(string c, string title);
 [DllImport("user32.dll")] public static extern int GetWindowLongW(IntPtr w, int index);
}
"@
$workspace = Split-Path $PSScriptRoot -Parent
$settings = Join-Path $workspace ('artifacts/experience-smoke-' + [guid]::NewGuid().ToString('N'))
$app = Start-Process -FilePath (Join-Path $workspace 'bin/Debug/net10.0-windows10.0.26100.0/Fish.exe') -ArgumentList "--settings-directory `"$settings`"" -WindowStyle Hidden -PassThru
try {
 Start-Sleep -Seconds 3
 $app.Refresh()
 if ($app.HasExited) { throw "App exited: $($app.ExitCode)" }
 $window = $app.MainWindowHandle
 $root = [System.Windows.Automation.AutomationElement]::FromHandle($window)
 function Find-Element([string]$name) { return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name)) }
 (Find-Element 'Anwendung').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 Start-Sleep -Milliseconds 300
 $theme = Find-Element 'Farbschema'
 $theme.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
 Start-Sleep -Milliseconds 300
 if (Find-Element 'System') { throw 'Removed System theme is still offered' }
 (Find-Element 'Light').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 $theme.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
 Start-Sleep -Milliseconds 500
 (Find-Element 'Immer im Vordergrund').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
 Start-Sleep -Seconds 1
 $saved=Get-Content (Join-Path $settings 'appsettings.json') -Raw | ConvertFrom-Json
 if ($saved.Theme -ne 'Light' -or $saved.AlwaysOnTop) { throw 'Experience preferences not saved' }
 if (([FishExperienceSmoke]::GetWindowLongW($window,-20) -band 8) -ne 0) { throw 'Always-on-top was not disabled' }
 Write-Output 'PASS Light theme saved; topmost removed immediately'
 foreach ($themeName in @('Dark')) {
   $theme = Find-Element 'Farbschema'
   $theme.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
   Start-Sleep -Milliseconds 300
   (Find-Element $themeName).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
   $theme.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
   Start-Sleep -Seconds 1
   if ((Get-Content (Join-Path $settings 'appsettings.json') -Raw | ConvertFrom-Json).Theme -ne $themeName) { throw "Theme $themeName not saved" }
 }
 Write-Output 'PASS Dark theme selection works without restart'
 (Find-Element 'Übersicht').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 Start-Sleep -Milliseconds 300
 foreach ($name in @('Protokoll', 'Live', 'Analyse')) {
   $elements = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name))
   $pattern = $null
   foreach ($element in $elements) {
     if ($element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) { break }
   }
   if (!$pattern) { throw "Missing expander: $name" }
   $pattern.Collapse()
   if ($pattern.Current.ExpandCollapseState -ne 'Collapsed') { throw "Cannot collapse $name" }
   $pattern.Expand()
   if ($pattern.Current.ExpandCollapseState -ne 'Expanded') { throw "Cannot expand $name" }
 }
 Write-Output 'PASS All three overview sections can collapse and expand'
 foreach ($name in @('Übersicht', 'Erkennung', 'Aktionen', 'Anwendung')) {
   if ((Find-Element $name).Current.ControlType.ProgrammaticName -ne 'ControlType.TabItem') { throw "Not a tab: $name" }
 }
 if (!(Find-Element '100 %') -or !(Find-Element '0 MB') -or !(Find-Element '0 ms')) { throw 'Graph Y-axis labels missing' }
 Write-Output 'PASS Native tabs and percent/MB/ms Y-axis labels'
 if ([FishExperienceSmoke]::FindWindowW($null, 'World of Warcraft') -eq [IntPtr]::Zero) {
   ($root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'StartButton'))).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
   Start-Sleep -Milliseconds 600
   if (!(Find-Element 'LÄUFT')) { throw 'Start feedback failed' }
   ($root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'StopButton'))).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
   Start-Sleep -Milliseconds 600
   if (!(Find-Element 'GESTOPPT')) { throw 'Stop feedback failed' }
   Write-Output 'PASS Start/Stop animation paths without a game window'
 } else { Write-Output 'SKIP Start/Stop: game window present' }
 Write-Output "Test settings: $settings"
} finally {
 $app.Refresh()
 if (!$app.HasExited) { $app.CloseMainWindow() | Out-Null; if (!$app.WaitForExit(3000)) { $app.Kill() } }
}
