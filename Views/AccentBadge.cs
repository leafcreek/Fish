using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Fish.Views;

// Theme-aware category chips; text remains meaningful without color.
public sealed class AccentBadge : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(AccentBadge), new PropertyMetadata("", Changed));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    private readonly TextBlock _text = new() { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly Border _border = new() { CornerRadius = new(5), Padding = new(6, 2, 6, 2), BorderThickness = new(1) };

    public AccentBadge()
    {
        _border.Child = _text; Content = _border;
        IsTabStop = false;
        ActualThemeChanged += (_, _) => Refresh();
        Loaded += (_, _) => Refresh();
    }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((AccentBadge)sender).Refresh();
    public void Refresh()
    {
        _text.Text = Label;
        var color = AccentColors.For(this, Label);
        _text.Foreground = new SolidColorBrush(color);
        _border.Background = new SolidColorBrush(Color.FromArgb(22, color.R, color.G, color.B));
        _border.BorderBrush = new SolidColorBrush(Color.FromArgb(65, color.R, color.G, color.B));
    }
}

internal static class AccentColors
{
    public static Color For(FrameworkElement element, string kind)
    {
        bool light = element.ActualTheme == ElementTheme.Light;
        return kind switch
        {
            "OK" or "Loot" or "Running" or "LÄUFT" => light ? Rgb(16, 112, 67) : Rgb(94, 224, 157),
            "FEHLER" or "Stopped" or "GESTOPPT" => light ? Rgb(183, 40, 64) : Rgb(255, 125, 145),
            "WARNUNG" or "Countdown" or "Aktion" or "Aktionen" => light ? Rgb(140, 83, 0) : Rgb(255, 197, 92),
            "Auto-Learn" or "Sitzung" => light ? Rgb(115, 62, 175) : Rgb(192, 155, 255),
            "Erkennung" or "Angeln" or "Bobber" => light ? Rgb(0, 107, 119) : Rgb(90, 215, 223),
            "Splash" => light ? Rgb(115, 62, 175) : Rgb(192, 155, 255),
            "Live" => light ? Rgb(16, 112, 67) : Rgb(94, 224, 157),
            "Inactive" => light ? Rgb(103, 113, 128) : Rgb(111, 125, 143),
            _ => light ? Rgb(36, 92, 177) : Rgb(125, 177, 255)
        };
    }
    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);
}
