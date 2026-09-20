using Fish.Configuration;
using System.Diagnostics;
using Fish.Core;
using Fish.Native;
using Fish.Services;
using Fish.ViewModels;
using Fish.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Graphics;

namespace Fish;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly SettingsStore _store;
    private BotOptions _options = new();
    private CancellationTokenSource? _cancellation;
    private Task? _session;
    private OverlayWindow? _bobberOverlay, _splashOverlay;
    private bool _closing, _closeAllowed, _saveAllowed = true;
    private BotUpdate? _displayed;
    private int _sessionId;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _topmostTimer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _overlayTimer;
    private readonly BotDesktop _overlayDesktop = new();
    private SessionMetrics _metrics = new();
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _performanceTimer;
    private TimeSpan? _lastCpu;
    private long _lastCpuTimestamp;

    public MainWindow(string? settingsDirectory = null)
    {
        _store = new(settingsDirectory);
        InitializeComponent();
        Title = "Fish";
        Root.DataContext = _viewModel;
        try { _options = _store.Load(); }
        catch (Exception ex) { _saveAllowed = false; ShowError("Einstellungen konnten nicht geladen werden. Die Datei bleibt erhalten. " + ex.Message); }
        _options.Normalize();
        RestoreWindowPlacement();
        RebuildSettings();
        _topmostTimer = DispatcherQueue.CreateTimer();
        _topmostTimer.Interval = TimeSpan.FromMilliseconds(500);
        _topmostTimer.Tick += (_, _) => ApplyTopmost();
        _topmostTimer.Start();
        _overlayTimer = DispatcherQueue.CreateTimer();
        _overlayTimer.Interval = TimeSpan.FromMilliseconds(150);
        _overlayTimer.Tick += (_, _) => UpdateOverlays();
        _overlayTimer.Start();
        _performanceTimer = DispatcherQueue.CreateTimer();
        _performanceTimer.Interval = TimeSpan.FromSeconds(1);
        _performanceTimer.Tick += (_, _) => RefreshPerformance();
        _performanceTimer.Start();
        RefreshPerformance();
        InitializeApplicationFeatures();
        InitializeExperience();
        AppWindow.Closing += OnClosing;
        AppWindow.Changed += (_, args) =>
        {
            if (args.DidPresenterChange || args.DidSizeChange) UpdateOverlays();
            if (args.DidSizeChange || args.DidPositionChange) ScheduleSave();
            if (_options.MinimizeToTray && AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized }) HideToTray();
        };
        _viewModel.AddLog("Fish bereit. WeakAura und Tastenbelegung vor dem Start prüfen.");
    }

    private void Section_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (OverviewPage == null || DetectionPage == null || FeaturesPage == null || ApplicationPage == null) return;
        OverviewPage.Visibility = SectionNavigation.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        DetectionPage.Visibility = SectionNavigation.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        FeaturesPage.Visibility = SectionNavigation.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        ApplicationPage.Visibility = SectionNavigation.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RebuildSettings()
    {
        SettingsEditor.Build(DetectionPanel, FeaturesPanel, _options, ScheduleSave);
        _viewModel.Tolerances = $"Toleranz · Bobber {_options.BobberAreaTolerance} · Splash {_options.SplashAreaTolerance}";
    }
    private void ApplyTopmost()
    {
        if (_closeAllowed || _closing) return;
        // Check our own window only. Avoid recursive presenter/activation notifications.
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        bool topmost = (Desktop.GetWindowLongW(handle, -20) & 0x00000008) != 0;
        if (topmost == _options.AlwaysOnTop) return;
        if (!Desktop.SetWindowPos(handle, _options.AlwaysOnTop ? -1 : -2, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010))
        {
            _topmostTimer.Stop();
            ShowError("Fish konnte nicht im Vordergrund gehalten werden.");
        }
    }
    private void SetRunning(bool running)
    {
        StartButton.IsEnabled = !running && !_closing;
        StopButton.IsEnabled = running;
        if (_tray != null) { _tray.Running = running; _tray.SetStatus(running ? "Sitzung aktiv" : "Bereit"); }
        ApplyMotion();
        foreach (var panel in new[] { DetectionPanel, FeaturesPanel })
            foreach (var control in panel.Children.OfType<Control>())
                control.IsEnabled = !running;
    }
    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_session is { IsCompleted: false } || _closing || _introductionOpen) return;
        _options.Normalize();
        MessageBar.IsOpen = false;
        _metrics = new();
        PerformancePanel.Reset();
        _cancellation = new();
        SetRunning(true);
        int id = ++_sessionId;
        _session = RunSessionAsync(id, _cancellation);
        await _session;
    }
    private async Task RunSessionAsync(int id, CancellationTokenSource cancellation)
    {
        var engine = new BotEngine(new BotDesktop());
        BotUpdate? pendingUpdate = null;
        int updateQueued = 0;
        int bobberTolerance = _options.BobberAreaTolerance, splashTolerance = _options.SplashAreaTolerance;
        var metrics = _metrics;
        engine.ScanMeasured += metrics.RecordScan;
        engine.Logged += entry =>
        {
            metrics.Record(entry);
            DispatcherQueue.TryEnqueue(() => { if (!_closeAllowed) { _viewModel.AddLog(entry); NotifyEntry(entry); } });
        };
        engine.Updated += update =>
        {
            bobberTolerance = update.BobberTolerance;
            splashTolerance = update.SplashTolerance;
            Interlocked.Exchange(ref pendingUpdate, update);
            if (Interlocked.Exchange(ref updateQueued, 1) == 0)
                DispatcherQueue.TryEnqueue(() =>
                {
                    Interlocked.Exchange(ref updateQueued, 0);
                    if (_closeAllowed || id != _sessionId) return;
                    var latest = Interlocked.Exchange(ref pendingUpdate, null);
                    try
                    {
                        if (latest != null) ApplyUpdate(latest);
                    }
                    catch (Exception ex) { _cancellation?.Cancel(); ShowError("Statusaktualisierung fehlgeschlagen: " + ex.Message); }
                });
        };
        try
        {
            _bobberOverlay ??= new OverlayWindow(0x0000ff);
            _splashOverlay ??= new OverlayWindow(0xfafafa);
            var snapshot = _options.Copy();
            await Task.Run(() => engine.RunAsync(snapshot, cancellation.Token));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex) { cancellation.Cancel(); ShowError("Angeln wurde gestoppt: " + ex.Message); }
        finally
        {
            cancellation.Cancel();
            _options.BobberAreaTolerance = bobberTolerance;
            _options.SplashAreaTolerance = splashTolerance;
            _sessionId++;

            _displayed = null;
            _viewModel.Status = "Bereit";
            _viewModel.Activity = "Gestoppt · keine ausstehenden Aktionen";
            SetRunning(false);
            RefreshPerformance();
            RebuildSettings();
            PersistChanges();
            cancellation.Dispose();
            if (_cancellation == cancellation) _cancellation = null;
            UpdateOverlays();
        }
    }
    private void RefreshPerformance()
    {
        if (_viewModel.FilterMinutes > 0) _viewModel.RefreshLogFilter();
        double? cpu = null, memory = null;
        try
        {
            _process.Refresh();
            var total = _process.TotalProcessorTime;
            long timestamp = Stopwatch.GetTimestamp();
            if (_lastCpu is { } previous)
            {
                double elapsed = Stopwatch.GetElapsedTime(_lastCpuTimestamp, timestamp).TotalSeconds;
                if (elapsed > 0) cpu = Math.Clamp((total - previous).TotalSeconds / elapsed / Environment.ProcessorCount * 100, 0, 100);
            }
            _lastCpu = total; _lastCpuTimestamp = timestamp;
            memory = _process.WorkingSet64 / (1024.0 * 1024);
        }
        catch (InvalidOperationException) { _lastCpu = null; }
        catch (System.ComponentModel.Win32Exception) { _lastCpu = null; }
        var now = DateTimeOffset.UtcNow;
        PerformancePanel.Refresh(_metrics.Snapshot(now), now, cpu, memory);
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (_cancellation is not { IsCancellationRequested: false }) return;
        _viewModel.AddLog("Stop angefordert. Laufende Sitzung und Countdowns werden abgebrochen.");
        _cancellation.Cancel();
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => _viewModel.Log.Clear();

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
            data.SetText(string.Join(Environment.NewLine, _viewModel.FilteredLog.Reverse()));
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
        }
        catch (Exception ex) { ShowError("Protokoll konnte nicht kopiert werden: " + ex.Message); }
    }
    private void ApplyUpdate(BotUpdate update)
    {
        bool statusChanged = _viewModel.Status != BotEngine.StateText(update.State);
        _viewModel.Status = BotEngine.StateText(update.State);
        if (statusChanged) Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(StatusText)?.RaiseAutomationEvent(Microsoft.UI.Xaml.Automation.Peers.AutomationEvents.LiveRegionChanged);
        _viewModel.Activity = update.Activity;
        _viewModel.Tolerances = $"Toleranz · Bobber {update.BobberTolerance} · Splash {update.SplashTolerance}";
        if (_options.BobberAreaTolerance != update.BobberTolerance || _options.SplashAreaTolerance != update.SplashTolerance)
        {
            _options.BobberAreaTolerance = update.BobberTolerance; _options.SplashAreaTolerance = update.SplashTolerance;
            ScheduleSave();
        }
        _tray?.SetStatus(_viewModel.Status);
        _displayed = update;
        UpdateOverlays();
    }
    private void UpdateOverlays()
    {
        if (_closing || _closeAllowed)
        { _bobberOverlay?.Hide(); _splashOverlay?.Hide(); return; }
        try
        {
            var game = _overlayDesktop.FindGame();
            if (!_overlayDesktop.IsAvailable(game) || _overlayDesktop.GetGameBounds(game) is not { } client)
            { _bobberOverlay?.Hide(); _splashOverlay?.Hide(); return; }
            var bobber = Detection.Around(new(client.X + client.Width / 2, client.Y + client.Height / 2),
                _options.BobberAreaWidth, _options.BobberAreaHeight, _options.BobberAreaXOffset, _options.BobberAreaYOffset, client);
            // Until a bobber is detected, show the splash size around the search-area center.
            var splash = _cancellation?.IsCancellationRequested == false && _displayed?.BobberArea == bobber
                ? _displayed.SplashArea : null;
            splash ??= Detection.Around(new(bobber.X + bobber.Width / 2, bobber.Y + bobber.Height / 2),
                _options.SplashAreaWidth, _options.SplashAreaHeight, _options.SplashAreaXOffset, _options.SplashAreaYOffset, client);
            _bobberOverlay ??= new OverlayWindow(0x0000ff);
            _splashOverlay ??= new OverlayWindow(0xfafafa);
            _bobberOverlay.Show(bobber);
            _splashOverlay.Show(bobber.Width > 0 && bobber.Height > 0 ? splash : null);
        }
        catch (Exception ex)
        {
            _overlayTimer.Stop();
            _bobberOverlay?.Hide(); _splashOverlay?.Hide();
            _cancellation?.Cancel(); ShowError(ex.Message);
        }
    }
    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeAllowed) return;
        if (!_exitRequested && _options.CloseToTray && _tray?.Available == true) { args.Cancel = true; HideToTray(); return; }
        if (_closing) { args.Cancel = true; return; }
        bool awaitSession = _session is { IsCompleted: false };
        args.Cancel = awaitSession;
        _closing = true;
        _saveTimer?.Stop();
        StartButton.IsEnabled = false;
        _cancellation?.Cancel();
        if (awaitSession) await _session!;
        try { if (_saveAllowed) SaveSettings(); }
        catch (Exception ex)
        {
            args.Cancel = true;
            _closing = false;
            RestoreWindow();
            SetRunning(false);
            ShowError("Einstellungen konnten nicht neben der EXE gespeichert werden. Fenster erneut schließen, um ohne Speichern zu beenden. " + ex.Message);
            _saveAllowed = false;
            return;
        }
        _bobberOverlay?.Dispose(); _splashOverlay?.Dispose();
        _topmostTimer.Stop();
        _overlayTimer.Stop();
        _performanceTimer.Stop();
        _process.Dispose();
        _applicationLifetime.Cancel();
        _tray?.Dispose();

        _closeAllowed = true;
        // An idle window can finish its original close; only an active session needs a second request.
        if (awaitSession) DispatcherQueue.TryEnqueue(Close);
    }
    private void SaveSettings()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
        {
            _options.WindowWidth = AppWindow.Size.Width; _options.WindowHeight = AppWindow.Size.Height;
            _options.WindowXPosition = AppWindow.Position.X; _options.WindowYPosition = AppWindow.Position.Y;
            _options.WindowDpi = (int)Desktop.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        }
        if (AppWindow.Presenter is OverlappedPresenter state && state.State != OverlappedPresenterState.Minimized)
            _options.WindowMaximized = state.State == OverlappedPresenterState.Maximized;
        _store.Save(_options);
        _saveAllowed = true;
    }
    private void ShowError(string message)
    {
        _lastError = message;
        MessageBar.Severity = InfoBarSeverity.Error; MessageBar.Message = message; MessageBar.IsOpen = true;
        var entry = new BotLogEntry(DateTimeOffset.Now, LogLevel.Error, "Fish", "app.error", message);
        _viewModel.AddLog(entry); NotifyEntry(entry);
        try { DiagnosticsService.Write(DiagnosticsFolder, DiagnosticsService.Report(_viewModel.Log.ToArray(), _metrics.Snapshot(DateTimeOffset.UtcNow), message)); }
        catch { /* The visible error remains available when the report cannot be written. */ }
    }
}
