using Fish.Core;
using Fish.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;

namespace Fish.Views;

public sealed partial class MainWindow
{
    private void SetRunning(bool running)
    {
        _running = running;
        ResetStepProgress();
        SessionButton.IsEnabled = !_closing;
        SetSessionAction(running ? "Stoppen" : "Starten", running);
        RefreshSessionAppearance();
    }

    private void SetSessionAction(string label, bool stop)
    {
        SessionButtonText.Text = label;
        SessionButtonIcon.Data = (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry),
            stop ? "M 0,0 L 12,0 L 12,12 L 0,12 Z" : "M 1,0 L 11,6 L 1,12 Z");
        AutomationProperties.SetName(SessionButton, label);
    }

    private async void Session_Click(object sender, RoutedEventArgs e)
    {
        if (_running) RequestStop();
        else await StartAsync();
    }

    private async Task StartAsync()
    {
        if (_closing || _running || _session is { IsCompleted: false }) return;
        _options.Normalize();
        MessageBar.IsOpen = false;
        _previews = new();
        PreviewPanel.Reset();
        _cancellation = new();
        SetRunning(true);
        int id = ++_sessionId;
        _session = RunSessionAsync(id, _cancellation);
        await _session;
    }

    private async Task RunSessionAsync(int id, CancellationTokenSource cancellation)
    {
        using var desktop = new BotDesktop();
        var engine = new BotEngine(desktop);
        engine.SetAreaEditing(_overlays.IsEditing);
        engine.SetLivePreviewEnabled(CanShowLivePreview);
        _activeEngine = engine;
        BotUpdate? pendingUpdate = null;
        int updateQueued = 0;
        var previews = _previews;
        engine.PreviewAvailable += (frame, kind, found) => previews.Add(frame, kind, found,
            includeLive: engine.LivePreviewEnabled);
        engine.Updated += update =>
        {
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
            var snapshot = _options.Copy();
            await Task.Run(() => engine.RunAsync(snapshot, cancellation.Token));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex) { cancellation.Cancel(); ShowError("Angeln wurde gestoppt: " + ex.Message); }
        finally
        {
            cancellation.Cancel();
            if (_activeEngine == engine) _activeEngine = null;
            _sessionId++;
            PreviewPanel.Refresh(_previews);
            PreviewPanel.ClearLive();
            _displayed = null;
            _viewModel.Status = "Bereit";
            _viewModel.Activity = "Mit Starten geht’s weiter.";
            SetRunning(false);
            PersistChanges();
            cancellation.Dispose();
            if (_cancellation == cancellation) _cancellation = null;
            UpdateOverlays();
        }
    }

    private void RequestStop()
    {
        if (_closing || _cancellation is not { IsCancellationRequested: false }) return;
        _cancellation.Cancel();
        SessionButton.IsEnabled = false;
        SetSessionAction("Stoppt …", true);
    }

    private void ApplyUpdate(BotUpdate update)
    {
        bool changed = _displayed?.State != update.State;
        _displayed = update;
        _viewModel.Status = update.State.Label();
        _viewModel.Activity = SessionText.Detail(update);
        UpdateStepProgress(update);
        if (changed) { RefreshSessionAppearance(); AnimateStatusChange(); }
        if (update.State is not (BotState.SearchingBobber or BotState.Fishing)) { _previews.ClearLive(); PreviewPanel.ClearLive(); }
        UpdateOverlays();
    }
}
