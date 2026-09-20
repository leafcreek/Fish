$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class FishExperienceSmoke {
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
 $skip = Find-Element 'Überspringen'
 if (!$skip) { throw 'First-start guide missing' }
 $skip.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Seconds 1
 if (!(Get-Content (Join-Path $settings 'appsettings.json') -Raw | ConvertFrom-Json).WelcomeCompleted) { throw 'Guide completion not saved' }
 Write-Output 'PASS First-start guide can be skipped and completion is saved'
 (Find-Element 'Anwendung').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 Start-Sleep -Milliseconds 300
 $theme = Find-Element 'Farbschema'
 $theme.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
 Start-Sleep -Milliseconds 300
 (Find-Element 'Light').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 $theme.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
 Start-Sleep -Milliseconds 500
 (Find-Element 'Bewegung reduzieren').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
 (Find-Element 'Immer im Vordergrund').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
 Start-Sleep -Seconds 1
 $saved=Get-Content (Join-Path $settings 'appsettings.json') -Raw | ConvertFrom-Json
 if ($saved.Theme -ne 'Light' -or !$saved.ReduceMotion -or $saved.AlwaysOnTop) { throw 'Experience preferences not saved' }
 if (([FishExperienceSmoke]::GetWindowLongW($window,-20) -band 8) -ne 0) { throw 'Always-on-top was not disabled' }
 Write-Output 'PASS Light theme and reduced motion saved; topmost removed immediately'
 foreach ($themeName in @('Dark', 'System')) {
   $theme = Find-Element 'Farbschema'
   $theme.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
   Start-Sleep -Milliseconds 300
   (Find-Element $themeName).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
   $theme.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
   Start-Sleep -Seconds 1
   if ((Get-Content (Join-Path $settings 'appsettings.json') -Raw | ConvertFrom-Json).Theme -ne $themeName) { throw "Theme $themeName not saved" }
 }
 Write-Output 'PASS Dark and System theme selections work without restart'
 (Find-Element 'Einführung anzeigen').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Milliseconds 500
 (Find-Element 'Überspringen').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Milliseconds 500
 Write-Output 'PASS Introduction can be reopened after first start'
 (Find-Element 'Übersicht').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 Start-Sleep -Milliseconds 300
 (Find-Element 'Protokoll durchsuchen').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('no-match-xyz')
 Start-Sleep -Milliseconds 300
 if (!(Find-Element 'Keine Treffer · Filter ändern oder zurücksetzen.')) { throw 'Empty search result not shown' }
 (Find-Element 'Protokollfilter zurücksetzen').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Write-Output 'PASS Log search and reset work in UI'
 Write-Output "Test settings: $settings"
} finally {
 $app.Refresh()
 if (!$app.HasExited) { $app.CloseMainWindow() | Out-Null; if (!$app.WaitForExit(3000)) { $app.Kill() } }
}
