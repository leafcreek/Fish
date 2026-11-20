param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][ValidateSet('Install','Check','Upgrade')][string]$Mode,
    [string]$ExpectedVersion,
    [switch]$CheckReleaseNotes
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UiHarness.ps1')
$workspace = Split-Path $PSScriptRoot -Parent
$folder = Join-Path $workspace ('artifacts\update-smoke\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$target = Join-Path $folder 'Fish.exe'
Copy-Item -LiteralPath (Resolve-Path $Executable).Path -Destination $target
$originalHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
$settings = Join-Path $folder 'settings'
$app = Start-Process -FilePath $target -ArgumentList @('--settings-directory',('"' + $settings + '"')) -WindowStyle Hidden -PassThru
$helper = $null; $restarted = $null; $sessionFolder = $null
try {
    $root = Wait-FishWindow $app
    if ($Mode -ne 'Install') {
        $version = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'VersionButton'))
        if (!$version) { throw 'Version button missing.' }
        $version.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    }
    if ($Mode -eq 'Check') {
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
    if ($Mode -eq 'Upgrade') {
        if (!$ExpectedVersion) { throw '-ExpectedVersion is required for Upgrade.' }
        $action = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::NameProperty,'Aktualisieren')
        $offer = $null
        for ($attempt=0; $attempt -lt 80; $attempt++) {
            $offer = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$action)
            if ($offer) { break }
            Start-Sleep -Milliseconds 200
        }
        if (!$offer) { throw 'Fish did not offer an update.' }
        if ($CheckReleaseNotes) {
            $byId = [System.Windows.Automation.AutomationElement]::AutomationIdProperty
            $children = [System.Windows.Automation.TreeScope]::Descendants
            $changesCondition = [System.Windows.Automation.PropertyCondition]::new($byId,'UpdateChanges')
            $notesCondition = [System.Windows.Automation.PropertyCondition]::new($byId,'UpdateReleaseNotes')
            $oldText = [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,'Beim Aktualisieren wird das Angeln beendet und Fish neu gestartet.')
            if ($root.FindFirst($children,$oldText)) { throw 'The update dialog still contains the old introductory text.' }
            $changes = $root.FindFirst($children,$changesCondition)
            if (!$changes) { throw 'The update dialog has no release notes expander.' }
            $expansion = $changes.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
            if ($expansion.Current.ExpandCollapseState -ne 'Collapsed') { throw 'Release notes should start collapsed.' }
            $expansion.Expand()
            $notes = $null
            for ($attempt=0; $attempt -lt 25; $attempt++) {
                $notes = $root.FindFirst($children,$notesCondition)
                if ($notes -and !$notes.Current.IsOffscreen) { break }
                Start-Sleep -Milliseconds 100
            }
            if (!$notes -or $notes.Current.IsOffscreen) { throw 'Expanding changes did not show the release notes.' }
            $value = $notes.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current
            if (!$value.IsReadOnly -or [string]::IsNullOrWhiteSpace($value.Value) -or
                $value.Value -like 'F*r diese Version sind keine * beschrieben.') {
                throw 'Expected the published release notes inside a read-only text box.'
            }
            $expansion.Collapse()
            Write-Output 'PASS release notes expand inside the update dialog in a read-only text box'
        }
        $offer.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        if (!$app.WaitForExit(60000)) { throw 'Fish did not close during upgrade.' }

        $cache = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Fish\Updates'
        foreach ($entry in (Get-ChildItem -LiteralPath $cache -Directory)) {
            $planPath = Join-Path $entry.FullName 'plan.json'
            if (!(Test-Path -LiteralPath $planPath)) { continue }
            try { $upgradePlan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json } catch { continue }
            if ($upgradePlan.Target -eq $target -and $upgradePlan.ParentId -eq $app.Id) {
                $sessionFolder = $entry.FullName
                break
            }
        }
        if (!$sessionFolder) { throw 'Upgrade did not create a handoff for the test executable.' }
        $helperPath = Join-Path $sessionFolder 'Fish.Updater.exe'
        $helper = Get-Process -Name Fish.Updater -ErrorAction SilentlyContinue |
            Where-Object Path -eq $helperPath | Select-Object -First 1
        if (!(Test-Path -LiteralPath (Join-Path $sessionFolder 'approved'))) {
            throw "Fish closed during upgrade without approving the handoff. Exit code: $($app.ExitCode)."
        }
        if ($app.ExitCode -ne 0) { throw "Fish exited during upgrade with code $($app.ExitCode)." }
        $startedFile = Join-Path $sessionFolder 'started'
        for ($attempt=0; $attempt -lt 150; $attempt++) {
            if (Test-Path -LiteralPath $startedFile) { break }
            Start-Sleep -Milliseconds 200
        }
        if (!(Test-Path -LiteralPath $startedFile)) { throw 'Upgrade did not restart Fish.' }
        $restarted = Get-Process -Id ([int][IO.File]::ReadAllText($startedFile))
        if ($restarted.MainModule.FileName -ne $target) { throw 'Upgrade restarted the wrong executable.' }
        $null = Wait-FishWindow $restarted
        $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($target).ProductVersion.Split('+')[0]
        if ($version -ne $ExpectedVersion.TrimStart('v') -or
            (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $upgradePlan.Sha256 -or
            (Get-FileHash -LiteralPath ($target+'.bak') -Algorithm SHA256).Hash -ne $originalHash -or
            !(Test-Path -LiteralPath (Join-Path $settings 'settings.json'))) {
            throw 'Unexpected installed version, original backup, or saved settings.'
        }
        Write-Output "PASS version click downloads, approves, replaces and restarts Fish $version"
        return
    }
    $session = [guid]::NewGuid().ToString('N')
    $sessionFolder = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Fish\Updates\' + $session)
    New-Item -ItemType Directory -Path $sessionFolder -Force | Out-Null
    $package = Join-Path $sessionFolder 'Fish.exe'
    $helperPath = Join-Path $sessionFolder 'Fish.Updater.exe'
    Copy-Item -LiteralPath $target -Destination $package
    Copy-Item -LiteralPath $target -Destination $helperPath
    $hash = $originalHash.ToLowerInvariant()
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
