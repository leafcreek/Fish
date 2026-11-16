using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI.ViewManagement;

namespace Fish.Views;

public sealed partial class MainWindow
{
    private readonly UISettings _uiSettings = new();

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
        AppWindow.TitleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ForegroundColor = Appearance.Text;
        AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = Appearance.Text;
        AppWindow.TitleBar.InactiveBackgroundColor = Appearance.Background;
        AppWindow.TitleBar.InactiveForegroundColor = Appearance.Text;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = Appearance.Text;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = Appearance.AccentSoft.Color;
        AppWindow.TitleBar.ButtonHoverForegroundColor = Appearance.Text;
        AppWindow.TitleBar.ButtonPressedBackgroundColor = Appearance.Track.Color;
        AppWindow.TitleBar.ButtonPressedForegroundColor = Appearance.Text;
        RefreshSessionAppearance();
        RefreshStepProgress();
    }
    private void RefreshSessionAppearance()
    {
        if (_closeAllowed) return;
        bool animate = _running && IsMainWindowVisible && _uiSettings.AnimationsEnabled;
        SessionProgress.IsActive = animate;
        SessionProgress.Visibility = animate ? Visibility.Visible : Visibility.Collapsed;
        StatusDot.Visibility = animate ? Visibility.Collapsed : Visibility.Visible;
        RefreshAccents();
    }
    private void RefreshAccents()
    {
        bool waiting = _running && _displayed?.State is Core.BotState.Fishing or Core.BotState.WoWNotRunning or Core.BotState.WoWNotActive;
        var status = waiting ? Appearance.Amber : Appearance.Detection;
        StatusDot.Fill = status;
        SessionProgress.Foreground = status;
        StatusBadge.Background = waiting ? Appearance.AmberSoft : Appearance.AccentSoft;
        SessionButton.Background = _running ? Appearance.Stopped : Appearance.Running;
        SessionButton.Foreground = SessionButtonIcon.Foreground = Appearance.White;
        SessionButton.Resources["ButtonBackgroundPointerOver"] = _running ? Appearance.StoppedHover : Appearance.RunningHover;
        SessionButton.Resources["ButtonBackgroundPressed"] = _running ? Appearance.StoppedPressed : Appearance.RunningPressed;
        SessionButton.Resources["ButtonForegroundPointerOver"] = Appearance.White;
        SessionButton.Resources["ButtonForegroundPressed"] = Appearance.White;
    }

    private void AnimateStatusChange()
    {
        if (!Root.IsLoaded || !IsMainWindowVisible || !_uiSettings.AnimationsEnabled) return;
        var visual = ElementCompositionPreview.GetElementVisual(StatusText);
        using var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0, .45f);
        animation.InsertKeyFrame(1, 1);
        animation.Duration = TimeSpan.FromMilliseconds(167);
        visual.StartAnimation("Opacity", animation);
    }

    private void AnimateCompletion()
    {
        if (!Root.IsLoaded || !IsMainWindowVisible || !_uiSettings.AnimationsEnabled) return;
        var visual = ElementCompositionPreview.GetElementVisual(StatusBadge);
        visual.CenterPoint = new((float)StatusBadge.ActualWidth / 2, (float)StatusBadge.ActualHeight / 2, 0);
        using var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(0, new(1, 1, 1));
        animation.InsertKeyFrame(.4f, new(1.12f, 1.12f, 1));
        animation.InsertKeyFrame(1, new(1, 1, 1));
        animation.Duration = TimeSpan.FromMilliseconds(250);
        visual.StartAnimation("Scale", animation);
    }
}
