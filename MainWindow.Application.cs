using Fish.Core;
using Fish.Native;
using Fish.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fish;

public sealed partial class MainWindow
{
    private TrayIcon? _tray;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _saveTimer;
    private readonly CancellationTokenSource _applicationLifetime = new();
    private bool _exitRequested, _saveErrorShown, _hiddenToTray;
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
                    case 6: EnsureWindowVisible(); break;
                    case 5: if (_options.MinimizeToTray) HideToTray(); break;
                }
            }));
            if (!_tray.Available) _viewModel.AddLog("Tray-Symbol konnte nicht angelegt werden; Fenster bleibt erreichbar.", LogLevel.Warning);
        }
        catch (Exception ex) { ShowError("Tray nicht verfügbar: " + ex.Message); }
        BindOption(MinimizeTrayOption, _options.MinimizeToTray, value => _options.MinimizeToTray = value);
        BindOption(CloseTrayOption, _options.CloseToTray, value => _options.CloseToTray = value);
        VersionLabel.Text = "Installierte Version: " + UpdateService.CurrentVersion;
        if (!_saveAllowed) SaveStatus.Text = "Speichern gesperrt: vorhandene Einstellungsdatei konnte nicht geladen werden. Datei prüfen und Fish neu starten.";
    }
    private void BindOption(CheckBox control, bool value, Action<bool> change)
    {
        control.IsChecked = value;
        control.Click += (_, _) => { change(control.IsChecked == true); ScheduleSave(); };
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
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        CheckUpdateButton.Content = "Wird geprüft …";
        try
        {
            var release = await UpdateService.CheckAsync(_applicationLifetime.Token);
            if (_closing || _closeAllowed) return;
            bool newer = release != null && release.Version > UpdateService.CurrentVersion;
            var dialog = new ContentDialog
            {
                XamlRoot = Root.XamlRoot, RequestedTheme = Root.RequestedTheme,
                Title = newer ? "Update verfügbar" : "Updates",
                Content = release == null ? "Noch keine öffentliche stabile Veröffentlichung verfügbar."
                    : newer ? $"Fish {release.Version} steht zum Download bereit." : $"Fish ist aktuell · Version {release.Version}.",
                CloseButtonText = "Schließen", PrimaryButtonText = newer ? "Release öffnen" : ""
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && release != null)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(release.Page.AbsoluteUri) { UseShellExecute = true });
        }
        catch (OperationCanceledException) { if (!_closing) ShowError("Updateprüfung abgebrochen oder Zeitlimit erreicht."); }
        catch (Exception ex) { if (!_closing) ShowError("Updateprüfung fehlgeschlagen: " + ex.Message); }
        finally
        {
            if (!_closeAllowed) { CheckUpdateButton.IsEnabled = true; CheckUpdateButton.Content = "Nach Updates suchen"; }
        }
    }
}