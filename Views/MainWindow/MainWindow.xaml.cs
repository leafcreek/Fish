using Fish.Configuration;
using Fish.Core;
using Fish.Native;
using Fish.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fish.Views;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly SettingsStore _store;
    private BotOptions _options = new();
    private CancellationTokenSource? _cancellation;
    private Task? _session;
    private BotEngine? _activeEngine;
    private bool _running;
    private readonly DetectionOverlays _overlays = new();
    private bool _closing, _closeAllowed, _saveAllowed = true;
    private bool _topmostErrorShown;
    private BotUpdate? _displayed;
    private int _sessionId;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _overlayTimer;
    private PreviewBuffer _previews = new();
    private bool IsMainWindowVisible => AppWindow.IsVisible &&
        AppWindow.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Minimized };

    public MainWindow(string? settingsDirectory = null)
    {
        _store = new(settingsDirectory);
        InitializeComponent();
        Title = "Fish";
        InitializeChrome();
        Root.DataContext = _viewModel;
        try { _options = _store.Load(); }
        catch (Exception ex) { _saveAllowed = false; ShowError("Einstellungen konnten nicht geladen werden. Die Datei bleibt erhalten. " + ex.Message); }
        _options.Normalize();
        _overlays.BobberRegionChanged += OverlayRegionChanged;
        _overlays.EditingChanged += editing => _activeEngine?.SetAreaEditing(editing);
        _overlays.InteractionFailed += error => { _activeEngine?.SetAreaEditing(false); ShowError("Suchbereich konnte nicht geändert werden: " + error.Message); };
        RestoreWindowPlacement();
        InitializeSettings();
        InitializePreviewLayout();
        _overlayTimer = DispatcherQueue.CreateTimer();
        _overlayTimer.Interval = TimeSpan.FromMilliseconds(150);
        _overlayTimer.Tick += (_, _) =>
        {
            UpdateOverlays();
            RefreshPreviews();
            RefreshStepProgress();
        };
        _overlayTimer.Start();
        InitializePersistence();
        InitializeWindowResources();
        InitializeAppearance();
        Activated += (_, _) => { ApplyTopmost(); RefreshSessionAppearance(); };
        AppWindow.Closing += OnClosing;
        AppWindow.Changed += (_, args) =>
        {
            if (args.DidPresenterChange) { ApplyTopmost(); RefreshSessionAppearance(); }
            if (args.DidSizeChange || args.DidPresenterChange) EnsureWindowVisible();
            if (args.DidPresenterChange || args.DidSizeChange) UpdateOverlays();
            if (args.DidSizeChange || args.DidPositionChange) ScheduleSave();
        };
    }

    private void ApplyTopmost()
    {
        if (_closeAllowed || _closing) return;
        try
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter && !presenter.IsAlwaysOnTop)
                presenter.IsAlwaysOnTop = true;
            WindowZOrder.EnsureTopmost(WinRT.Interop.WindowNative.GetWindowHandle(this), true);
            _topmostErrorShown = false;
        }
        catch (Exception ex)
        {
            if (!_topmostErrorShown) ShowError("Vordergrundstatus konnte nicht geändert werden: " + ex.Message);
            _topmostErrorShown = true;
        }
    }
    private void UpdateOverlays()
    {
        if (_closing || _closeAllowed || !IsMainWindowVisible)
        { _overlays.Hide(); return; }
        try
        {
            _overlays.Update(_options, _displayed, _cancellation?.IsCancellationRequested == false);
            ApplyTopmost();
        }
        catch (Exception ex)
        {
            _overlayTimer.Stop();
            _overlays.Hide();
            _cancellation?.Cancel(); ShowError(ex.Message);
        }
    }
    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeAllowed) return;
        if (_closing) { args.Cancel = true; return; }
        bool awaitSession = _session is { IsCompleted: false };
        args.Cancel = awaitSession;
        _closing = true;
        _updateCancellation?.Cancel();
        _updateDialog?.Hide();
        _updateStatusFlyout?.Hide();
        _saveTimer?.Stop();
        SessionButton.IsEnabled = false;
        _cancellation?.Cancel();
        if (awaitSession) await _session!;
        try
        {
            if (_saveAllowed) SaveSettings();
            _updateHandoff?.Approve();
            _updateHandoff?.Dispose();
            _updateHandoff = null;
        }
        catch (Exception ex)
        {
            _updateHandoff?.Dispose();
            _updateHandoff = null;
            args.Cancel = true;
            _closing = false;
            RestoreWindow();
            SetRunning(false);
            ShowError("Einstellungen konnten nicht gespeichert werden. Fenster erneut schließen, um ohne Speichern zu beenden. " + ex.Message);
            _saveAllowed = false;
            return;
        }
        _overlays.Dispose();
        _overlayTimer.Stop();
        _windowEnvironment?.Dispose();

        _closeAllowed = true;
        // An idle window can finish its original close; only an active session needs a second request.
        if (awaitSession) DispatcherQueue.TryEnqueue(Close);
    }
    private void ShowError(string message)
    {
        MessageBar.Severity = InfoBarSeverity.Error; MessageBar.Message = message; MessageBar.IsOpen = true;
    }
}
