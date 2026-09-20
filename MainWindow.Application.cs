using Fish.Core;
using Fish.Native;
using Fish.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Fish;

public sealed partial class MainWindow
{
    private TrayIcon? _tray;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _saveTimer;
    private readonly CancellationTokenSource _applicationLifetime = new();
    private bool _exitRequested, _saveErrorShown, _hiddenToTray;
    private string? _lastError;
    private readonly Dictionary<string, DateTimeOffset> _lastNotifications = new();
    private string DiagnosticsFolder => Path.Combine(_store.Folder, "diagnostics");

    private void InitializeApplicationFeatures()
    {
        _saveTimer = DispatcherQueue.CreateTimer();
        _saveTimer.Interval = TimeSpan.FromMilliseconds(400);
        _saveTimer.IsRepeating = false;
        _saveTimer.Tick += (_, _) => PersistChanges();
        try
        {
            _tray = new(WinRT.Interop.WindowNative.GetWindowHandle(this), command => DispatcherQueue.TryEnqueue(() =>
            {
                if (_closing || _closeAllowed) return;
                switch (command)
                {
                    case 1: RestoreWindow(); break;
                    case 2: Start_Click(this, new RoutedEventArgs()); break;
                    case 3: Stop_Click(this, new RoutedEventArgs()); break;
                    case 4: ExitApplication(); break;
                    case 7: RefreshAppearance(); ApplyMotion(); break;
                    case 6: EnsureWindowVisible(); break;
                    case 5: if (_options.MinimizeToTray) HideToTray(); break;
                }
            }));
            if (!_tray.Available) _viewModel.AddLog("Tray-Symbol konnte nicht angelegt werden; Fenster bleibt erreichbar.", LogLevel.Warning);
        }
        catch (Exception ex) { ShowError("Tray nicht verfügbar: " + ex.Message); }
        BindOption(MinimizeTrayOption, _options.MinimizeToTray, value => _options.MinimizeToTray = value);
        BindOption(CloseTrayOption, _options.CloseToTray, value => _options.CloseToTray = value);
        BindOption(HotkeyOption, _options.GlobalStopHotkey, value => { _options.GlobalStopHotkey = value; ConfigureHotkey(); });
        BindOption(ErrorNotifyOption, _options.NotifyErrors, value => _options.NotifyErrors = value);
        BindOption(ActionNotifyOption, _options.NotifyActions, value => _options.NotifyActions = value);
        BindOption(EndNotifyOption, _options.NotifySessionEnd, value => _options.NotifySessionEnd = value);
        BindOption(SoundNotifyOption, _options.NotificationSound, value => _options.NotificationSound = value);
        ConfigureHotkey();
        VersionLabel.Text = "Installierte Version: " + UpdateService.CurrentVersion;
        RepositoryBox.Text = _options.UpdateRepository;
        RepositoryBox.TextChanged += (_, _) => { _options.UpdateRepository = RepositoryBox.Text.Trim(); ScheduleSave(); };
        if (!_saveAllowed) SaveStatus.Text = "Speichern gesperrt: vorhandene Einstellungsdatei konnte nicht geladen werden. Datei prüfen und Fish neu starten.";
        Shortcut(VirtualKey.F5, VirtualKeyModifiers.None, () => Start_Click(this, new RoutedEventArgs()));
        Shortcut(VirtualKey.F5, VirtualKeyModifiers.Shift, () => Stop_Click(this, new RoutedEventArgs()));
        Shortcut(VirtualKey.C, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, () => CopyLog_Click(this, new RoutedEventArgs()));
        Shortcut(VirtualKey.M, VirtualKeyModifiers.Control, HideToTray);
        Shortcut(VirtualKey.Q, VirtualKeyModifiers.Control, ExitApplication);
        for (int i = 0; i < 4; i++)
        {
            int section = i;
            Shortcut((VirtualKey)((int)VirtualKey.Number1 + i), VirtualKeyModifiers.Control, () => SectionNavigation.SelectedIndex = section);
        }
    }
    private void BindOption(CheckBox control, bool value, Action<bool> change)
    {
        control.IsChecked = value;
        control.Click += (_, _) => { change(control.IsChecked == true); ScheduleSave(); };
    }
    private void Shortcut(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, args) => { if (!_closing) action(); args.Handled = true; };
        Root.KeyboardAccelerators.Add(accelerator);
    }
    private void ConfigureHotkey()
    {
        bool registered = _tray?.SetHotkey(_options.GlobalStopHotkey) == true;
        HotkeyStatus.Text = !_options.GlobalStopHotkey ? "Globaler Hotkey ausgeschaltet." : registered
            ? "Globaler Stop aktiv, auch wenn Fish im Tray liegt."
            : "Hotkey nicht verfügbar (möglicherweise bereits belegt). Stop im Fenster oder Tray verwenden.";
    }
    private void RestoreWindow()
    {
        bool maximize = _options.WindowMaximized;
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.Restore();
        EnsureWindowVisible();
        if (maximize && AppWindow.Presenter is OverlappedPresenter maximized) maximized.Maximize();
        AppWindow.Show();
        _hiddenToTray = false;
        Activate();
        Desktop.SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }
    private void HideToTray()
    {
        if (_tray?.Available != true) { RestoreWindow(); return; }
        if (_hiddenToTray) return;
        PersistChanges();
        _hiddenToTray = true;
        AppWindow.Hide();
    }
    private void ExitApplication() { _exitRequested = true; Close(); }
    private void ScheduleSave()
    {
        if (!_saveAllowed || _closing || _closeAllowed || _saveTimer == null) return;
        SaveStatus.Text = "Änderungen werden gespeichert …";
        _saveTimer.Stop(); _saveTimer.Start();
    }
    private void PersistChanges()
    {
        if (!_saveAllowed || _closeAllowed) return;
        try
        {
            SaveSettings(); _saveErrorShown = false;
            SaveStatus.Text = "Gespeichert · " + DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            SaveStatus.Text = "Nicht gespeichert. Schreibrechte und freien Speicher prüfen; die nächste Änderung versucht es erneut.";
            if (!_saveErrorShown) { _saveErrorShown = true; ShowError("Automatisches Speichern fehlgeschlagen: " + ex.Message); }
        }
    }
    private void NotifyEntry(BotLogEntry entry)
    {
        bool show = entry.Level == LogLevel.Error && _options.NotifyErrors ||
            entry.EventId.StartsWith("action.", StringComparison.Ordinal) && _options.NotifyActions ||
            entry.EventId == "session.ended" && _options.NotifySessionEnd;
        if (!show) return;
        var now = DateTimeOffset.UtcNow;
        if (_lastNotifications.TryGetValue(entry.EventId, out var last) && (now - last).TotalSeconds < 10) return;
        _lastNotifications[entry.EventId] = now;
        _tray?.Notify("Fish · " + entry.Category, entry.Message, _options.NotificationSound, entry.Level == LogLevel.Error);
    }
    private void TestNotification_Click(object sender, RoutedEventArgs e)
    {
        if (_tray?.Available != true) { ShowError("Benachrichtigungen benötigen ein verfügbares Tray-Symbol."); return; }
        _tray.Notify("Fish", "Benachrichtigungen sind eingerichtet.", _options.NotificationSound);
    }
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false; ReleaseLink.Visibility = Visibility.Collapsed; ReleaseNotes.Text = "";
        UpdateStatus.Text = "GitHub-Releases werden geprüft …";
        try
        {
            var release = await UpdateService.CheckAsync(RepositoryBox.Text, _applicationLifetime.Token);
            if (_closing || _closeAllowed) return;
            if (release == null) { UpdateStatus.Text = "Keine öffentliche stabile Veröffentlichung gefunden. Projektadresse und GitHub-Releases prüfen."; return; }
            UpdateStatus.Text = release.Version > UpdateService.CurrentVersion
                ? $"Neue Version {release.Version} verfügbar. Download und Installation erfolgen über die Release-Seite."
                : $"Aktuell · neueste stabile Veröffentlichung: {release.Version}.";
            ReleaseNotes.Text = release.Notes; ReleaseLink.NavigateUri = release.Page; ReleaseLink.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException) { if (!_closing) UpdateStatus.Text = "Updateprüfung abgebrochen oder Zeitlimit erreicht."; }
        catch (Exception ex) { UpdateStatus.Text = "Updateprüfung fehlgeschlagen: " + ex.Message; }
        finally { if (!_closeAllowed) CheckUpdateButton.IsEnabled = true; }
    }
    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string report = DiagnosticsService.Report(_viewModel.Log.ToArray(), _metrics.Snapshot(DateTimeOffset.UtcNow), _lastError);
            string path = DiagnosticsService.Write(DiagnosticsFolder, report);
            DiagnosticsStatus.Text = "Gespeichert: " + path;
        }
        catch (Exception ex) { ShowError("Diagnosebericht konnte nicht gespeichert werden: " + ex.Message); }
    }
    private void OpenDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(DiagnosticsFolder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(DiagnosticsFolder) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError("Diagnoseordner konnte nicht geöffnet werden: " + ex.Message); }
    }
    internal void RecordUnhandledException(Exception exception)
    {
        try { DiagnosticsService.Write(DiagnosticsFolder, "Fish " + UpdateService.CurrentVersion + "\nUnbehandelter Fehler\n" + exception); }
        catch { /* Preserve the original failure if reporting is impossible. */ }
    }
}
