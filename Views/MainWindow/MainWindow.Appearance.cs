using Microsoft.UI.Xaml;

namespace Fish.Views;

public sealed partial class MainWindow
{
    private void InitializeAppearance()
    {
        RefreshAppearance(); ApplyTopmost();
        Root.Loaded += (_, _) =>
        {
            RefreshAppearance();
        };
    }
    private void RefreshAppearance()
    {
        if (_closeAllowed) return;
        AppWindow.TitleBar.BackgroundColor = Appearance.Background;
        AppWindow.TitleBar.ForegroundColor = Appearance.Text;
        AppWindow.TitleBar.ButtonBackgroundColor = Appearance.Background;
        AppWindow.TitleBar.ButtonForegroundColor = Appearance.Text;
        AppWindow.TitleBar.InactiveBackgroundColor = Appearance.Background;
        AppWindow.TitleBar.InactiveForegroundColor = Appearance.Text;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Appearance.Background;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = Appearance.Text;
        RefreshSessionAppearance();
        RefreshStepProgress();
    }
    private void RefreshSessionAppearance()
    {
        if (_closeAllowed) return;
        bool animate = _running;
        SessionProgress.IsActive = animate;
        SessionProgress.Visibility = animate ? Visibility.Visible : Visibility.Collapsed;
        StatusDot.Visibility = animate ? Visibility.Collapsed : Visibility.Visible;
        RefreshAccents();
    }
    private void RefreshAccents()
    {
        var status = _running ? Appearance.Running : Appearance.Stopped;
        StatusDot.Fill = status;
        SessionProgress.Foreground = status;
        StatusAccent.Background = status;
        SessionButton.Foreground = _running ? Appearance.Stopped : Appearance.Running;
        SessionButtonIcon.Foreground = SessionButton.Foreground;
    }
}
