using Fish.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Fish;

public sealed partial class MainWindow
{
    private Storyboard? _lampPulse;
    private bool _lampAnimating;
    private Storyboard? _actionFeedback;

    private void AnimateAction(bool start)
    {
        _actionFeedback?.Stop();
        var target = start ? StartIndicator : StopIndicator;
        var transform = new CompositeTransform();
        target.RenderTransform = transform;
        target.RenderTransformOrigin = new Windows.Foundation.Point(.5, .5);
        var storyboard = new Storyboard();
        if (start)
        {
            // A short spring on Start, independent of the ongoing lamp pulse.
            foreach (string property in new[] { "ScaleX", "ScaleY" })
            {
                var spring = new DoubleAnimation
                {
                    From = .88, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(450)),
                    EasingFunction = new BackEase { Amplitude = .6, EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(spring, transform); Storyboard.SetTargetProperty(spring, property);
                storyboard.Children.Add(spring);
            }
        }
        else
        {
            // Stop gives a small sideways impulse and settles back in place.
            var settle = new DoubleAnimationUsingKeyFrames();
            foreach (var (milliseconds, offset) in new[] { (0, 0.0), (70, 4.0), (150, -3.0), (230, 1.0), (320, 0.0) })
                settle.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds)), Value = offset });
            Storyboard.SetTarget(settle, transform); Storyboard.SetTargetProperty(settle, "TranslateX");
            storyboard.Children.Add(settle);
        }
        _actionFeedback = storyboard;
        storyboard.Begin();
    }
    private void RefreshAccents()
    {
        if (StartLamp == null) return;
        bool running = StopButton.IsEnabled;
        var active = new SolidColorBrush(AccentColors.For(Root, running ? "Running" : "Stopped"));
        var inactive = new SolidColorBrush(AccentColors.For(Root, "Inactive"));
        StartLamp.Fill = running ? active : inactive;
        StopLamp.Fill = running ? inactive : active;
        StartLamp.Opacity = running ? 1 : .35;
        StopLamp.Opacity = running ? .35 : 1;
        bool animate = running;
        if (_lampAnimating != animate)
        {
            _lampPulse?.Stop();
            _lampAnimating = animate;
            if (animate)
            {
                var pulse = new DoubleAnimation { From = .5, To = 1, Duration = new Duration(TimeSpan.FromSeconds(1.2)), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                Storyboard.SetTarget(pulse, StartLamp);
                Storyboard.SetTargetProperty(pulse, "Opacity");
                _lampPulse = new Storyboard();
                _lampPulse.Children.Add(pulse);
                _lampPulse.Begin();
            }
        }
        StartIndicator.BorderBrush = running ? active : inactive;
        StopIndicator.BorderBrush = running ? inactive : active;
        SessionBadge.Label = running ? "LÄUFT" : "GESTOPPT";
        SessionBadge.Visibility = Visibility.Visible;
        StatusDot.Fill = active;
        SessionProgress.Foreground = active;
        StatusAccent.Background = active;
        BrandIcon.Foreground = new SolidColorBrush(AccentColors.For(Root, "Erkennung"));
    }

}
