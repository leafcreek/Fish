param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UiHarness.ps1')
$exe = (Resolve-Path -LiteralPath $Executable).Path
$target = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Fish\settings.json'
$legacy = Join-Path (Split-Path -Parent $exe) 'appsettings.json'
$hashes = @{}
foreach ($file in @($legacy,($legacy + '.bak'))) {
    if (Test-Path -LiteralPath $file) { $hashes[$file] = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
}
$previous = if (Test-Path -LiteralPath $target) { Get-Content -LiteralPath $target -Raw | ConvertFrom-Json } elseif (Test-Path -LiteralPath $legacy) { Get-Content -LiteralPath $legacy -Raw | ConvertFrom-Json } else { $null }
# This intentionally exercises the real default location and performs the user's requested migration.
# No settings controls are changed and the session is never started.
$app = Start-Process -FilePath $exe -WindowStyle Hidden -PassThru
try {
    $root = Wait-FishWindow $app
    Start-Sleep -Milliseconds 500
    $errorBar = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'MessageBar'))
    if ($errorBar -and !$errorBar.Current.IsOffscreen -and $errorBar.Current.BoundingRectangle.Height -gt 0) { throw 'Default settings could not be loaded.' }
    $root.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (!$app.WaitForExit(10000) -or $app.ExitCode -ne 0) { throw 'Default Fish did not close cleanly.' }
    if (!(Test-Path -LiteralPath $target)) { throw 'LocalAppData settings.json was not created.' }
    $saved = Get-Content -LiteralPath $target -Raw | ConvertFrom-Json
    $allowed = @('SchemaVersion','FishingHotkey','WindowXPosition','WindowYPosition','BobberAreaWidth','BobberAreaHeight','BobberAreaXOffset','BobberAreaYOffset','BobberSensitivity','SplashSensitivity')
    if ($saved.SchemaVersion -ne 7 -or @($saved.PSObject.Properties.Name | Where-Object { $_ -notin $allowed }).Count -ne 0) { throw 'Removed settings remain in the real configuration.' }
    if ($previous) {
        foreach ($name in @('FishingHotkey','BobberAreaWidth','BobberAreaHeight','BobberAreaXOffset','BobberAreaYOffset')) {
            if ($previous.PSObject.Properties.Name -contains $name -and $saved.$name -ne $previous.$name) { throw "User setting was not preserved: $name" }
        }
    }
    foreach ($file in $hashes.Keys) {
        if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $hashes[$file]) { throw 'Legacy settings were modified.' }
    }
    if (!(Test-Path -LiteralPath ($target + '.bak'))) { throw 'Default settings backup is missing.' }
    Write-Output 'PASS real LocalAppData path, schema 7, preserved user controls, legacy files unchanged, no fixed settings'
    Write-Output "Settings: $target"
} finally {
    $app.Refresh()
    if (!$app.HasExited) { $app.Kill(); $app.WaitForExit() }
    $app.Dispose()
}