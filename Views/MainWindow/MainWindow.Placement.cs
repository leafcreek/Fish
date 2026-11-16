using Fish.Configuration;
using Fish.Core;
using Fish.Native;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Fish.Views;

public sealed partial class MainWindow
{
    private const int ExpandedWindowHeight = 820;
    private bool _adjustingWindow;
    private WindowEnvironment? _windowEnvironment;
    private WindowIcon? _windowIcon;

    private void InitializeWindowResources()
    {
        try { _windowIcon = new(WinRT.Interop.WindowNative.GetWindowHandle(this)); }
        catch (Exception ex) { ShowError("Fenstersymbol nicht verfügbar: " + ex.Message); }
        Closed += (_, _) => _windowIcon?.Dispose();
        try
        {
            _windowEnvironment = new(WinRT.Interop.WindowNative.GetWindowHandle(this), () => DispatcherQueue.TryEnqueue(EnsureWindowVisible));
        }
        catch (Exception ex) { ShowError("Bildschirmüberwachung nicht verfügbar: " + ex.Message); }
    }
    private void RestoreWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.Restore();
        EnsureWindowVisible();
        AppWindow.Show();
        Activate();
        Desktop.SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        UpdateOverlays();
    }
    private void RestoreWindowPlacement()
    {
        try
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
            }
            var saved = new PixelRect(_options.WindowXPosition, _options.WindowYPosition, BotOptions.DefaultWindowWidth, BotOptions.DefaultWindowHeight);
            var fitted = FitWindow(DesiredWindowBounds(saved));
            AppWindow.Move(new PointInt32(fitted.X, fitted.Y));
            fitted = FitWindow(DesiredWindowBounds(fitted));
            AppWindow.MoveAndResize(new RectInt32(fitted.X, fitted.Y, fitted.Width, fitted.Height));
        }
        catch (Exception ex) { ShowError("Fensterposition konnte nicht wiederhergestellt werden: " + ex.Message); }
    }
    private PixelRect DesiredWindowBounds(PixelRect current)
    {
        double scale = Math.Max(96, Desktop.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))) / 96.0;
        return current with
        {
            Width = (int)Math.Round(BotOptions.DefaultWindowWidth * scale),
            Height = (int)Math.Round((PreviewSection.IsExpanded ? ExpandedWindowHeight : BotOptions.DefaultWindowHeight) * scale)
        };
    }
    private PixelRect FitWindow(PixelRect saved, PixelRect? anchor = null)
    {
        var monitors = Desktop.Monitors();
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.First();
        double scale = Math.Max(96, Desktop.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))) / 96.0;
        return WindowPlacement.Fit(saved, monitors.Select(m => m.WorkArea ?? m.Bounds).ToArray(), primary.WorkArea ?? primary.Bounds,
            (int)Math.Ceiling(Configuration.BotOptions.MinimumWindowWidth * scale), (int)Math.Ceiling(Configuration.BotOptions.MinimumWindowHeight * scale), anchor);
    }
    private void EnsureWindowVisible()
    {
        if (_adjustingWindow || _closing || _closeAllowed || AppWindow.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Restored }) return;
        try
        {
            _adjustingWindow = true;
            var current = new PixelRect(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
            var fitted = FitWindow(DesiredWindowBounds(current), anchor: current);
            if (fitted != current) AppWindow.MoveAndResize(new RectInt32(fitted.X, fitted.Y, fitted.Width, fitted.Height));
        }
        catch (Exception ex) { ShowError("Fenster konnte nicht in den sichtbaren Bereich verschoben werden: " + ex.Message); }
        finally { _adjustingWindow = false; }
    }
}
