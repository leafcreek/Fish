using Fish.Native;
using Microsoft.UI.Windowing;

namespace Fish.Views;

public sealed partial class MainWindow
{
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _saveTimer;
    private bool _saveErrorShown;
    private void InitializePersistence()
    {
        _saveTimer = DispatcherQueue.CreateTimer();
        _saveTimer.Interval = TimeSpan.FromMilliseconds(400);
        _saveTimer.IsRepeating = false;
        _saveTimer.Tick += (_, _) => PersistChanges();
    }
    private void ScheduleSave()
    {
        if (!_saveAllowed || _closing || _closeAllowed || _saveTimer == null) return;
        _saveTimer.Stop(); _saveTimer.Start();
    }
    private void PersistChanges()
    {
        if (!_saveAllowed || _closeAllowed) return;
        try { SaveSettings(); _saveErrorShown = false; }
        catch (Exception ex)
        {
            if (!_saveErrorShown) { _saveErrorShown = true; ShowError("Automatisches Speichern fehlgeschlagen: " + ex.Message); }
        }
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
}
