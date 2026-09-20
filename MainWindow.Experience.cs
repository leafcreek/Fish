using Fish.Core;
using Fish.Native;
using Fish.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;

namespace Fish;

public sealed partial class MainWindow
{
    private bool _adjustingWindow;

    private void InitializeExperience()
    {
        ThemeOption.SelectedIndex = _options.Theme == "Light" ? 1 : 0;
        ThemeOption.SelectionChanged += (_, _) =>
        {
            _options.Theme = ThemeOption.SelectedIndex == 1 ? "Light" : "Dark";
            ApplyThemeChoice(); ScheduleSave();
        };
        BindOption(AlwaysOnTopOption, _options.AlwaysOnTop, value => { _options.AlwaysOnTop = value; ApplyTopmost(); });
        Root.ActualThemeChanged += (_, _) => RefreshAppearance();
        ApplyThemeChoice(); ApplyMotion();
        Root.Loaded += (_, _) =>
        {
            ApplyMotion(); RefreshAppearance();
        };
    }
    private void ApplyThemeChoice()
    {
        Root.RequestedTheme = _options.Theme == "Light" ? ElementTheme.Light : ElementTheme.Dark;
        RefreshAppearance();
    }
    private void RefreshAppearance()
    {
        if (_closeAllowed) return;
        var colors = ThemePalette.For(Root);
        AppWindow.TitleBar.BackgroundColor = colors.Background;
        AppWindow.TitleBar.ForegroundColor = colors.Text;
        AppWindow.TitleBar.ButtonBackgroundColor = colors.Background;
        AppWindow.TitleBar.ButtonForegroundColor = colors.Text;
        AppWindow.TitleBar.InactiveBackgroundColor = colors.Background;
        AppWindow.TitleBar.InactiveForegroundColor = colors.Text;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = colors.Background;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = colors.Text;
        PerformancePanel.ApplyTheme();
        RefreshAccents();
    }
    private void ApplyMotion()
    {
        if (_closeAllowed) return;
        foreach (var page in new FrameworkElement[] { OverviewContent, DetectionPage, FeaturesPage })
            page.Transitions = new TransitionCollection { new EntranceThemeTransition() };
        bool animate = StopButton.IsEnabled;
        SessionProgress.IsActive = animate;
        SessionProgress.Visibility = animate ? Visibility.Visible : Visibility.Collapsed;
        StatusDot.Visibility = animate ? Visibility.Collapsed : Visibility.Visible;
        RefreshAccents();
    }
    private void RestoreWindowPlacement()
    {
        try
        {
            var saved = new PixelRect(_options.WindowXPosition, _options.WindowYPosition, _options.WindowWidth, _options.WindowHeight);
            var fitted = FitWindow(saved);
            AppWindow.Move(new PointInt32(fitted.X, fitted.Y));
            uint dpi = Desktop.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
            if (_options.WindowDpi > 0 && dpi > 0)
                fitted = FitWindow(fitted with { Width = (int)Math.Round((double)_options.WindowWidth * dpi / _options.WindowDpi), Height = (int)Math.Round((double)_options.WindowHeight * dpi / _options.WindowDpi) });
            AppWindow.MoveAndResize(new RectInt32(fitted.X, fitted.Y, fitted.Width, fitted.Height));
            if (_options.WindowMaximized && AppWindow.Presenter is OverlappedPresenter presenter) presenter.Maximize();
        }
        catch (Exception ex) { ShowError("Fensterposition konnte nicht wiederhergestellt werden: " + ex.Message); }
    }
    private static PixelRect FitWindow(PixelRect saved)
    {
        var monitors = Desktop.Monitors();
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.First();
        return WindowPlacement.Fit(saved, monitors.Select(m => m.WorkArea ?? m.Bounds).ToArray(), primary.WorkArea ?? primary.Bounds);
    }
    private void EnsureWindowVisible()
    {
        if (_adjustingWindow || _closing || _closeAllowed || AppWindow.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Restored }) return;
        try
        {
            _adjustingWindow = true;
            var current = new PixelRect(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
            var fitted = FitWindow(current);
            if (fitted != current) AppWindow.MoveAndResize(new RectInt32(fitted.X, fitted.Y, fitted.Width, fitted.Height));
        }
        catch (Exception ex) { ShowError("Fenster konnte nicht in den sichtbaren Bereich verschoben werden: " + ex.Message); }
        finally { _adjustingWindow = false; }
    }
}
