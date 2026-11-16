param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][ValidateSet('Install','Check')][string]$Mode
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UiHarness.ps1')
$workspace = Split-Path $PSScriptRoot -Parent
$folder = Join-Path $workspace ('artifacts\update-smoke\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$target = Join-Path $folder 'Fish.exe'
Copy-Item -LiteralPath (Resolve-Path $Executable).Path -Destination $target
$settings = Join-Path $folder 'settings'
$app = Start-Process -FilePath $target -ArgumentList @('--settings-directory',('"' + $settings + '"')) -WindowStyle Hidden -PassThru
$helper = $null; $restarted = $null; $sessionFolder = $null
try {
    $root = Wait-FishWindow $app
    if ($Mode -eq 'Check') {
        $version = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'VersionButton'))
        if (!$version) { throw 'Version button missing.' }
        $version.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        $message = $null
        for ($attempt=0; $attempt -lt 80; $attempt++) {
            $message = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
                [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Alles aktuell'))
            if ($message) { break }
            Start-Sleep -Milliseconds 200
        }
        if (!$message) { throw 'Live update check did not report the published version as current.' }
        Write-Output 'PASS version click queries public GitHub release and reports current version without authentication'
        return
    }
    $session = [guid]::NewGuid().ToString('N')
    $sessionFolder = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Fish\Updates\' + $session)
    New-Item -ItemType Directory -Path $sessionFolder -Force | Out-Null
    $package = Join-Path $sessionFolder 'Fish.exe'
    $helperPath = Join-Path $sessionFolder 'Fish.Updater.exe'
    Copy-Item -LiteralPath $target -Destination $package
    Copy-Item -LiteralPath $target -Destination $helperPath
    $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    $plan = @{ Target=$target; Sha256=$hash; OriginalSha256=$hash; ParentId=$app.Id;
        ParentStarted=$app.StartTime.ToUniversalTime().Ticks; SettingsDirectory=$settings }
    [IO.File]::WriteAllText((Join-Path $sessionFolder 'plan.json'),($plan | ConvertTo-Json))
    $helper = Start-Process -FilePath $helperPath -ArgumentList @('--apply-update',$session) -WindowStyle Hidden -PassThru
    for ($attempt=0; $attempt -lt 100; $attempt++) {
        if (Test-Path -LiteralPath (Join-Path $sessionFolder 'ready')) { break }
        if ($helper.HasExited) { throw 'Updater exited before readiness handshake.' }
        Start-Sleep -Milliseconds 200
    }
    if (!(Test-Path -LiteralPath (Join-Path $sessionFolder 'ready'))) { throw 'Updater readiness timed out.' }
    [IO.File]::WriteAllText((Join-Path $sessionFolder 'approved'),'ok')
    $root.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (!$helper.WaitForExit(45000)) { throw 'Updater did not complete.' }
    if ($helper.ExitCode -ne 0) {
        $detail = if (Test-Path (Join-Path $sessionFolder 'error.txt')) { Get-Content (Join-Path $sessionFolder 'error.txt') -Raw } else { 'No error detail.' }
        throw "Updater failed: $detail"
    }
    $started = Join-Path $sessionFolder 'started'
    if (!(Test-Path -LiteralPath $started)) { throw 'Restart acknowledgement missing.' }
    $restarted = Get-Process -Id ([int][IO.File]::ReadAllText($started))
    if ($restarted.MainModule.FileName -ne $target) { throw 'Restarted the wrong executable.' }
    $newRoot = Wait-FishWindow $restarted
    if ((Get-FileHash -LiteralPath ($target+'.bak') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash -or
        !(Test-Path -LiteralPath (Join-Path $settings 'settings.json'))) { throw 'Backup or preserved settings folder missing.' }
    Write-Output 'PASS copied updater waits for parent, replaces isolated EXE, preserves backup/settings and confirms restarted WinUI window'
} finally {
    foreach ($process in @($app,$restarted)) {
        if ($process -and !$process.HasExited) {
            $process.CloseMainWindow() | Out-Null
            if (!$process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit() }
        }
    }
    if ($helper -and !$helper.HasExited) {
        [IO.File]::WriteAllText((Join-Path $sessionFolder 'cancelled'),'cancel')
        if (!$helper.WaitForExit(2000)) { $helper.Kill(); $helper.WaitForExit() }
    }
    if ($sessionFolder -and (Split-Path $sessionFolder -Leaf) -match '^[0-9a-f]{32}$') {
        foreach ($name in @('Fish.exe','Fish.Updater.exe','plan.json','ready','approved','cancelled','started','error.txt')) {
            $path = Join-Path $sessionFolder $name
            if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
        }
        if (!(Get-ChildItem -LiteralPath $sessionFolder -Force)) { Remove-Item -LiteralPath $sessionFolder }
    }
}
